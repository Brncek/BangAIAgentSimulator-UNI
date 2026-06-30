using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;


namespace BangSimulatorLib.Agent
{
    public class DQNAgentNet : IAgent
    {
        private DqnBrain? brain;
        
        private float[]? prevState;
        private float[]? prevMask;
        private int prevAction = -1;
        private bool hasPending;

        private float pendingReward;

        public bool Training { get; set; } = true;

        private bool eval = false;

        private List<float> rewards = [];

        private string savePath = string.Empty;
        private double lastRewardAVG = 0.0;

        public AgentAction Step(GameInfo gameInfo)
        {
            var (state, mask) = gameInfo.Encode();
            lastRole = gameInfo.PlayerRole;

            brain ??= new DqnBrain(stateSize: state.Length,
                                    actionSize: gameInfo.ActionSpaceSize);

            if (hasPending && Training)
            {
                brain.Remember(new Transition(
                    prevState!, prevAction, pendingReward,
                    state, mask, done: false));
                
                if (!eval)
                {
                    brain.Learn();
                }
            }

            int action = brain.SelectAction(state, mask, greedy: !Training);

            prevState = state;
            prevMask = mask;
            prevAction = action;
            hasPending = true;
            pendingReward = 0f;

            var (cardType, target, endTurn) = gameInfo.DecodeAction(action);
            if (endTurn || cardType == null)
                return new AgentAction { PlayedCard = null, target = 0 };

            Card? realCard = FindRealCard(gameInfo, cardType.Value, target);
            return new AgentAction { PlayedCard = realCard, target = target };
        }

        private static Card? FindRealCard(GameInfo info, CardBangType type, int target)
        {
            foreach (var action in info.AvanableActions)
            {
                if (action.PlayedCard?.Type == type &&
                    Array.IndexOf(action.PotencialTargets, target) >= 0)
                    return action.PlayedCard;
            }
            return null;
        }

        public void AddReward(float r) => pendingReward += r;

        public void GameOver(PlayerRole winingRole)
        {
            if (!hasPending || !Training || brain == null) return;

            if (lastRole == PlayerRole.Deputy) lastRole = PlayerRole.Sheriff;

            float finalReward = pendingReward +
                (prevState != null && winingRole == lastRole ? 1f : -1f);


            brain.Remember(new Transition(
                prevState!, prevAction, finalReward,
                prevState!, prevMask!, done: true));
            
            if(!eval)
            {
                brain.Learn();
            }

            rewards.Add(finalReward);

            if (!string.IsNullOrEmpty(savePath))
            {
                double avg;

                if (rewards.Count > 100) avg = rewards.TakeLast(100).Sum() / 100.0;
                else avg = rewards.Sum() / (float)rewards.Count();

                if (avg > lastRewardAVG)
                {
                    lastRewardAVG = avg;
                    try
                    {
                        InternalSave(savePath); //NOTE: notifi if unable to save
                    }
                    catch
                    { }
                }
            }


            hasPending = false;
        }

        private PlayerRole lastRole;
        public void Reset()
        {
            eval = false;
            hasPending = false;
            prevState = null;
            prevMask = null;
            prevAction = -1;
            pendingReward = 0f;
        }

        public void SetAutoSavePath(string path)
        {
            savePath = path;
        }

        public void InternalSave(string path) => brain?.Save(path);
        public void Load(string path)
        {
            brain?.Load(path);
        }

        public bool HasReward() => true;

        public void SetEval(bool eval)
        {
            this.eval = eval;
        }

        public List<float> GetRewards()
        {
            return rewards;
        }
    }


    public readonly struct Transition(float[] s, int a, float r, float[] ns, float[] nm, bool done)
    {
        public readonly float[] State = s;
        public readonly int Action = a;
        public readonly float Reward = r;
        public readonly float[] NextState = ns;
        public readonly float[] NextMask = nm;
        public readonly bool Done = done;
    }

    public sealed class DqnBrain
    {
        private readonly int stateSize;
        private readonly int actionSize;
        private readonly Device _device;

        private readonly Sequential policyNet;
        private readonly Sequential targetNet;
        private readonly optim.Optimizer optimizer;

        private readonly List<Transition> memory = [];
        private readonly int memoryCapacity;
        private int memoryHead;

        private readonly Random rng = GlobalRnd.Rnd;

        private readonly int batchSize;
        private readonly float gamma;
        private readonly float epsStart, epsEnd, epsDecay;
        private readonly int targetSyncEvery;

        private int stepsDone;
        private int learnCount;

        public DqnBrain( 
            int stateSize,
            int actionSize,
            int width = 256,
            int memoryCapacity = 50_000,
            int batchSize = 64,
            float gamma = 0.99f,
            float lr = 1e-4f,
            float epsStart = 1.0f,
            float epsEnd = 0.05f,
            float epsDecay = 20_000f,
            int targetSyncEvery = 1_000,
            bool useCuda = true)
        {
            this.stateSize = stateSize;
            this.actionSize = actionSize;
            this.memoryCapacity = memoryCapacity;
            this.batchSize = batchSize;
            this.gamma = gamma;
            this.epsStart = epsStart;
            this.epsEnd = epsEnd;
            this.epsDecay = epsDecay;
            this.targetSyncEvery = targetSyncEvery;

            _device = (useCuda && cuda.is_available()) ? CUDA : CPU;

            policyNet = BuildNet(stateSize, actionSize, width).to(_device);
            targetNet = BuildNet(stateSize, actionSize, width).to(_device);
            CopyWeights(policyNet, targetNet);
            targetNet.eval();

            optimizer = optim.Adam(policyNet.parameters(), lr: lr);
        }

        private static Sequential BuildNet(int inSize, int outSize, int width) =>
            Sequential(
                ("fc1", Linear(inSize, width)),
                ("relu1", ReLU()),
                ("fc2", Linear(width, width)),
                ("relu2", ReLU()),
                ("out", Linear(width, outSize))
            );

        public int SelectAction(float[] state, float[] mask, bool greedy = false)
        {
            float eps = epsEnd + (epsStart - epsEnd) *
                        MathF.Exp(-stepsDone / epsDecay);
            stepsDone++;

            if (!greedy && rng.NextDouble() < eps)
                return RandomLegal(mask);

            using var _ = no_grad();
            using var s = tensor(state, [1, stateSize], device: _device);
            using var q = policyNet.forward(s).squeeze(0);         
            using var m = tensor(mask, [actionSize], device: _device);
            
            using var neg = full_like(q, -1e9f);
            using var masked = where(m > 0.5f, q, neg);
            return (int)masked.argmax().item<long>();
        }

        private int RandomLegal(float[] mask)
        {
            Span<int> legal = stackalloc int[mask.Length];
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i] > 0.5f) legal[n++] = i;
            return n == 0 ? actionSize - 1 : legal[rng.Next(n)];
        }

        public void Remember(in Transition t)
        {
            if (memory.Count < memoryCapacity)
                memory.Add(t);
            else
            {
                memory[memoryHead] = t;
                memoryHead = (memoryHead + 1) % memoryCapacity;
            }
        }

        public float Learn()
        {
            if (memory.Count < batchSize) return 0f;

            var batch = new Transition[batchSize];
            for (int i = 0; i < batchSize; i++)
                batch[i] = memory[rng.Next(memory.Count)];

            var states = new float[batchSize * stateSize];
            var nextStates = new float[batchSize * stateSize];
            var nextMasks = new float[batchSize * actionSize];
            var actions = new long[batchSize];
            var rewards = new float[batchSize];
            var notDone = new float[batchSize];

            for (int i = 0; i < batchSize; i++)
            {
                Array.Copy(batch[i].State, 0, states, i * stateSize, stateSize);
                Array.Copy(batch[i].NextState, 0, nextStates, i * stateSize, stateSize);
                Array.Copy(batch[i].NextMask, 0, nextMasks, i * actionSize, actionSize);
                actions[i] = batch[i].Action;
                rewards[i] = batch[i].Reward;
                notDone[i] = batch[i].Done ? 0f : 1f;
            }

            using var s = tensor(states, [batchSize, stateSize], device: _device);
            using var ns = tensor(nextStates, [batchSize, stateSize], device: _device);
            using var nm = tensor(nextMasks, [batchSize, actionSize], device: _device);
            using var a = tensor(actions, [batchSize, 1], device: _device);
            using var r = tensor(rewards, [batchSize], device: _device);
            using var nd = tensor(notDone, [batchSize], device: _device);

            using var qAll = policyNet.forward(s);                  
            using var qTaken = qAll.gather(1, a).squeeze(1);         

            Tensor target;
            using (no_grad())
            {
                using var qNext = targetNet.forward(ns);            
                using var neg = full_like(qNext, -1e9f);
                using var qNextMasked = where(nm > 0.5f, qNext, neg);
                using var qNextMax = qNextMasked.max(1).values;     

                target = r + gamma * qNextMax * nd;                 
            }

            using var loss = functional.smooth_l1_loss(qTaken, target);
            optimizer.zero_grad();
            loss.backward();
            optimizer.step();
            target.Dispose();

            float lossVal = loss.item<float>();

            if (++learnCount % targetSyncEvery == 0)
                CopyWeights(policyNet, targetNet);

            return lossVal;
        }

        private static void CopyWeights(Module<Tensor, Tensor> from, Module<Tensor, Tensor> to)
        {
            using var _ = no_grad();
            var src = from.state_dict();
            to.load_state_dict(src);
        }

        public void Save(string path) => policyNet.save(path);
        public void Load(string path)
        {
            policyNet.load(path);
            CopyWeights(policyNet, targetNet);
        }
    }
}
