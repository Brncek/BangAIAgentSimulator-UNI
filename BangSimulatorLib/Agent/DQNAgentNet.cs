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
        private DqnBrain? _brain;

        // Holds the last (state, action, mask) so we can build a Transition once
        // the reward for that action is known (on the NEXT Step, or at GameOver).
        private float[]? _prevState;
        private float[]? _prevMask;
        private int _prevAction = -1;
        private bool _hasPending;

        // Reward accumulated since the last action (set by your engine between turns).
        private float _pendingReward;

        // Toggle off for evaluation/play to disable exploration + learning.
        public bool Training { get; set; } = true;

        public AgentAction Step(GameInfo gameInfo)
        {
            var (state, mask) = gameInfo.Encode();
            _lastRole = gameInfo.PlayerRole;

            // Lazily build the brain once we know the true vector sizes.
            _brain ??= new DqnBrain(stateSize: state.Length,
                                    actionSize: gameInfo.ActionSpaceSize);

            // Close out the previous transition: prev -> (reward) -> current state.
            if (_hasPending && Training)
            {
                _brain.Remember(new Transition(
                    _prevState!, _prevAction, _pendingReward,
                    state, mask, done: false));
                _brain.Learn();
            }

            // Choose this turn's action (masked).
            int action = _brain.SelectAction(state, mask, greedy: !Training);

            // Stash for next-step transition.
            _prevState = state;
            _prevMask = mask;
            _prevAction = action;
            _hasPending = true;
            _pendingReward = 0f;

            // Decode to a real game action.
            var (cardType, target, endTurn) = gameInfo.DecodeAction(action);
            if (endTurn || cardType == null)
                return new AgentAction { PlayedCard = null, target = 0 };

            Card? realCard = FindRealCard(gameInfo, cardType.Value, target);
            return new AgentAction { PlayedCard = realCard, target = target };
        }

        // Reuse the actual Card instance from the engine so hashcode/reference
        // identity matches what BangSimulator expects.
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

        // Call from your engine between turns to credit the last action.
        // e.g. AddReward(+0.1) when it deals damage, AddReward(-0.05) per life lost.
        public void AddReward(float r) => _pendingReward += r;

        public void GameOver(PlayerRole winingRole)
        {
            if (!_hasPending || !Training || _brain == null) return;

            float finalReward = _pendingReward +
                (_prevState != null && winingRole == _lastRole ? 1f : -1f);

            // Terminal transition: next state/mask are irrelevant (Done = true),
            // pass the prev state as a placeholder for next.
            _brain.Remember(new Transition(
                _prevState!, _prevAction, finalReward,
                _prevState!, _prevMask!, done: true));
            _brain.Learn();

            _hasPending = false;
        }

        // Track own role to score the terminal reward.
        private PlayerRole _lastRole;
        public void Reset()
        {
            _hasPending = false;
            _prevState = null;
            _prevMask = null;
            _prevAction = -1;
            _pendingReward = 0f;
        }

        // Optional: persistence
        public void Save(string path) => _brain?.Save(path);
        public void Load(string path)
        {
            // Note: brain must exist (one Step must have run) before loading,
            // since sizes are inferred. Or construct it explicitly if you know sizes.
            _brain?.Load(path);
        }
    }




    // A single transition stored in replay memory.
    // NextMask is kept so the Bellman target masks illegal actions in s'.
    public readonly struct Transition
    {
        public readonly float[] State;
        public readonly int Action;
        public readonly float Reward;
        public readonly float[] NextState;
        public readonly float[] NextMask;
        public readonly bool Done;

        public Transition(float[] s, int a, float r, float[] ns, float[] nm, bool done)
        {
            State = s; Action = a; Reward = r; NextState = ns; NextMask = nm; Done = done;
        }
    }

    public sealed class DqnBrain
    {
        private readonly int _stateSize;
        private readonly int _actionSize;
        private readonly Device _device;

        private readonly Sequential _policyNet;
        private readonly Sequential _targetNet;
        private readonly optim.Optimizer _optimizer;

        private readonly List<Transition> _memory = new();
        private readonly int _memoryCapacity;
        private int _memoryHead;

        private readonly Random _rng = new();

        // Hyperparameters
        private readonly int _batchSize;
        private readonly float _gamma;
        private readonly float _epsStart, _epsEnd, _epsDecay;
        private readonly int _targetSyncEvery;

        private int _stepsDone;
        private int _learnCount;

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
            _stateSize = stateSize;
            _actionSize = actionSize;
            _memoryCapacity = memoryCapacity;
            _batchSize = batchSize;
            _gamma = gamma;
            _epsStart = epsStart;
            _epsEnd = epsEnd;
            _epsDecay = epsDecay;
            _targetSyncEvery = targetSyncEvery;

            _device = (useCuda && cuda.is_available()) ? CUDA : CPU;

            _policyNet = BuildNet(stateSize, actionSize, width).to(_device);
            _targetNet = BuildNet(stateSize, actionSize, width).to(_device);
            CopyWeights(_policyNet, _targetNet);
            _targetNet.eval();

            _optimizer = optim.Adam(_policyNet.parameters(), lr: lr);
        }

        private static Sequential BuildNet(int inSize, int outSize, int width) =>
            Sequential(
                ("fc1", Linear(inSize, width)),
                ("relu1", ReLU()),
                ("fc2", Linear(width, width)),
                ("relu2", ReLU()),
                ("out", Linear(width, outSize))
            );

        // ---- Action selection with TRUE masking ----
        // mask: 1f for legal actions, 0f otherwise. Length == actionSize.
        public int SelectAction(float[] state, float[] mask, bool greedy = false)
        {
            float eps = _epsEnd + (_epsStart - _epsEnd) *
                        MathF.Exp(-_stepsDone / _epsDecay);
            _stepsDone++;

            // Exploration: pick uniformly among LEGAL actions only.
            if (!greedy && _rng.NextDouble() < eps)
                return RandomLegal(mask);

            using var _ = no_grad();
            using var s = tensor(state, new long[] { 1, _stateSize }, device: _device);
            using var q = _policyNet.forward(s).squeeze(0);          // [actionSize]
            using var m = tensor(mask, new long[] { _actionSize }, device: _device);

            // Set Q of illegal actions to a large finite negative so argmax
            // can never choose them (finite avoids any NaN/inf propagation).
            using var neg = full_like(q, -1e9f);
            using var masked = where(m > 0.5f, q, neg);
            return (int)masked.argmax().item<long>();
        }

        private int RandomLegal(float[] mask)
        {
            // Collect legal indices, pick one at random. Falls back to end-turn.
            Span<int> legal = stackalloc int[mask.Length];
            int n = 0;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i] > 0.5f) legal[n++] = i;
            return n == 0 ? _actionSize - 1 : legal[_rng.Next(n)];
        }

        // ---- Memory ----
        public void Remember(in Transition t)
        {
            if (_memory.Count < _memoryCapacity)
                _memory.Add(t);
            else
            {
                _memory[_memoryHead] = t;
                _memoryHead = (_memoryHead + 1) % _memoryCapacity;
            }
        }

        // ---- One gradient step on a random minibatch ----
        public float Learn()
        {
            if (_memory.Count < _batchSize) return 0f;

            // Sample a minibatch.
            var batch = new Transition[_batchSize];
            for (int i = 0; i < _batchSize; i++)
                batch[i] = _memory[_rng.Next(_memory.Count)];

            // Flatten into contiguous arrays for tensor construction.
            var states = new float[_batchSize * _stateSize];
            var nextStates = new float[_batchSize * _stateSize];
            var nextMasks = new float[_batchSize * _actionSize];
            var actions = new long[_batchSize];
            var rewards = new float[_batchSize];
            var notDone = new float[_batchSize];

            for (int i = 0; i < _batchSize; i++)
            {
                Array.Copy(batch[i].State, 0, states, i * _stateSize, _stateSize);
                Array.Copy(batch[i].NextState, 0, nextStates, i * _stateSize, _stateSize);
                Array.Copy(batch[i].NextMask, 0, nextMasks, i * _actionSize, _actionSize);
                actions[i] = batch[i].Action;
                rewards[i] = batch[i].Reward;
                notDone[i] = batch[i].Done ? 0f : 1f;
            }

            using var s = tensor(states, new long[] { _batchSize, _stateSize }, device: _device);
            using var ns = tensor(nextStates, new long[] { _batchSize, _stateSize }, device: _device);
            using var nm = tensor(nextMasks, new long[] { _batchSize, _actionSize }, device: _device);
            using var a = tensor(actions, new long[] { _batchSize, 1 }, device: _device);
            using var r = tensor(rewards, new long[] { _batchSize }, device: _device);
            using var nd = tensor(notDone, new long[] { _batchSize }, device: _device);

            // Q(s,a) for the actions actually taken (needs grad).
            using var qAll = _policyNet.forward(s);                  // [B, A]
            using var qTaken = qAll.gather(1, a).squeeze(1);         // [B]

            // Target: r + gamma * max_a' Q_target(s', a') over LEGAL a'.
            // Computed under no_grad so it's treated as a constant.
            Tensor target;
            using (no_grad())
            {
                using var qNext = _targetNet.forward(ns);            // [B, A]
                using var neg = full_like(qNext, -1e9f);
                using var qNextMasked = where(nm > 0.5f, qNext, neg);
                using var qNextMax = qNextMasked.max(1).values;      // [B]
                                                                     // end-turn is always legal, so at least one action is unmasked
                                                                     // and the max is always a real Q-value (never the -1e9 sentinel).
                target = r + _gamma * qNextMax * nd;                 // [B]
            }

            // Loss + backward MUST be outside no_grad so gradients flow.
            using var loss = functional.smooth_l1_loss(qTaken, target);
            _optimizer.zero_grad();
            loss.backward();
            _optimizer.step();
            target.Dispose();

            float lossVal = loss.item<float>();

            if (++_learnCount % _targetSyncEvery == 0)
                CopyWeights(_policyNet, _targetNet);

            return lossVal;
        }

        private static void CopyWeights(Module<Tensor, Tensor> from, Module<Tensor, Tensor> to)
        {
            using var _ = no_grad();
            var src = from.state_dict();
            to.load_state_dict(src);
        }

        public void Save(string path) => _policyNet.save(path);
        public void Load(string path)
        {
            _policyNet.load(path);
            CopyWeights(_policyNet, _targetNet);
        }
    }
}
