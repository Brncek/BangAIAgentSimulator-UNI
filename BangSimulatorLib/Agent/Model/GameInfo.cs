using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent.Model
{
    public class GameInfo
    {
        public PlayerRole PlayerRole { get; set; }

        public int[] GamePlayerLifes { get; set; } = [];

        public int ScherifId { get; set; }

        public int PlayerHelth { get; set; } = 0;

        public List<Action> AvanableActions {  get; set; } = []; 

        public List<Card> CardsOut { get; set; } = [];

        public DeckMemory[] DeckMemory { get; set; } = [];

        public int NumTargets => GamePlayerLifes.Length + 2;
        public int ActionSpaceSize => Enum.GetValues(typeof(CardBangType)).Length * NumTargets + 1; // +1 = end turn
        public int EndTurnIndex => ActionSpaceSize - 1;


        public (float[] State, float[] Mask) Encode(bool includeMemory)
        {
            List<float> features = [];

            features.AddRange(OneHot((int)PlayerRole, 0, Enum.GetValues(typeof(PlayerRole)).Length - 1));
            features.AddRange(GamePlayerLifes.Select(l => (float)l));

            features.AddRange(OneHot(ScherifId, 0, GamePlayerLifes.Length - 1)); 
            
            //---CARDSOUT

            float[] cardsOut = Enumerable.Repeat(0f, 10).ToArray();

            foreach (var card in CardsOut)
            {
                cardsOut[(int)card.Type] = 1f;
            }

            features.AddRange(cardsOut);

            //---DECK MEMORY

            if (includeMemory)
            {

                int memorySize = Math.Max(0, Deck.MemSize);
                int playerCount = GamePlayerLifes.Length;

                int cardTypeCount = Enum.GetValues(typeof(CardBangType)).Length;
                int cardValueCount = Enum.GetValues(typeof(CardValue)).Length;
                int cardSuitCount = Enum.GetValues(typeof(CardSuit)).Length;

                int eventSize = 1 + cardTypeCount + playerCount
                                  + NumTargets + cardValueCount + cardSuitCount;

                var recentMemory = (this.DeckMemory ?? Array.Empty<DeckMemory>())
                    .Where(m => m != null && m.plaied != null)
                    .TakeLast(memorySize)
                    .ToArray();

                int padding = memorySize - recentMemory.Length;

                for (int slot = 0; slot < memorySize; slot++)
                {
                    if (slot < padding)
                    {
                        features.AddRange(Enumerable.Repeat(0f, eventSize));
                        continue;
                    }

                    var item = recentMemory[slot - padding];

                    features.Add(1f); 
                    features.AddRange(OneHot((int)item.plaied.Type, 0, cardTypeCount - 1));
                    features.AddRange(OneHot(item.pId, 0, playerCount - 1));

                    features.AddRange(OneHot(item.targetId, -2, playerCount - 1));

                    features.AddRange(OneHot((int)item.plaied.Value, 0, cardValueCount - 1));
                    features.AddRange(OneHot((int)item.plaied.Suit, 0, cardSuitCount - 1));
                }
            }

            return (features.ToArray(), BuildMask());
        }


        private float[] BuildMask()
        {
            float[] mask = new float[ActionSpaceSize];

            foreach (var action in AvanableActions)
            {
                if (action.PlayedCard == null)
                {
                    mask[EndTurnIndex] = 1f;
                    continue;
                }

                foreach (var t in action.PotencialTargets)
                {
                    int slot = TargetSlot(t);
                    if (slot < 0 || slot >= NumTargets) continue; 
                    mask[ActionIndex(action.PlayedCard.Type, t)] = 1f;
                }
            }

            return mask;
        }

        private int TargetSlot(int target) => target + 2;

        private int ActionIndex(CardBangType card, int target)
            => (int)card * NumTargets + TargetSlot(target);

        // Decode a flat action index back into (card, target) for stepping the env.
        // Use this on the agent side after argmax / sampling.
        public (CardBangType? Card, int Target, bool EndTurn) DecodeAction(int index)
        {
            if (index == EndTurnIndex) return (null, 0, true);
            int card = index / NumTargets;
            int slot = index % NumTargets;
            return ((CardBangType)card, slot - 2, false);
        }

        private float[] OneHot(int data, int minNum, int maxNum)
        {
            int range = maxNum - minNum + 1;
            float[] oneHot = new float[range];
            if (data >= minNum && data <= maxNum)
            {
                oneHot[data - minNum] = 1f;
            }
            return oneHot;
        }
    }


    public class Action
    {
        public Card? PlayedCard { get; set; }
        public int[] PotencialTargets { get; set; } = [];

        public override string ToString()
        {
            return "PlayedCard: " + (PlayedCard != null ? PlayedCard.ToString() : "None") + ", PotencialTargets: [" + string.Join(", ", PotencialTargets) + "]";
        }
    }
}
