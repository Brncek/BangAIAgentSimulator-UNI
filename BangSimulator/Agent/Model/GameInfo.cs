using BangSimulator.Game;

namespace BangSimulator.Agent.Model
{
    public class GameInfo
    {
        private static float NoData = -10f;

        public PlayerRole PlayerRole { get; set; }

        public int[] GamePlayerLifes { get; set; } = [];

        public int ScherifId { get; set; }

        public int PlayerHelth { get; set; } = 0;

        public List<Action> AvanableActions {  get; set; } = []; 

        public List<Card> CardsOut { get; set; } = [];

        public DeckMemory[] DeckMemory { get; set; } = [];


        public float[] Encode(bool includeMemory = false)
        {
            //FIXME: REDO ENCODING 

            List<float> features = [];

            features.Add((float)PlayerRole);
            features.AddRange(GamePlayerLifes.Select(l => (float)l));
            features.Add((float)ScherifId);

            for (int i = 0; i < 3; i++)
            {
                if (i < CardsOut.Count)
                {
                    features.Add((float)CardsOut[i].Type);
                }
                else
                {
                    features.Add(NoData); // No card
                }
            }

            for (int i = 0; 10 > i; i++)
            {
                if (i < AvanableActions.Count)
                {
                    var action = AvanableActions[i];
                    features.Add(action.PlayedCard != null ? (float)action.PlayedCard.Type : -1f);
                    
                    var targets = action.PotencialTargets;
                    
                    for (int j = 0; j < GamePlayerLifes.Length + 2; j++)
                    {
                        if (j < targets.Length)
                        {
                            features.Add((float)targets[j]);
                        }
                        else
                        {
                            features.Add(NoData); // No target
                        }
                    }
                }
                else
                {
                    features.Add(-10f); // No card played
                    features.AddRange(Enumerable.Repeat(NoData, GamePlayerLifes.Length + 2)); // No targets
                }
            }

            if (includeMemory)
            {
                for (int i = 0; i < Deck.MemSize; i++)
                {
                    if (i >= DeckMemory.Length)
                    {
                        features.AddRange(Enumerable.Repeat(NoData, 3)); // No memory
                        continue;
                    }

                    var memory = DeckMemory[i];
                    features.Add((float)memory.plaied.Type);
                    features.Add((float)memory.pId);
                    features.Add((float)memory.targetId);
                }
            }


            return features.ToArray();
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
