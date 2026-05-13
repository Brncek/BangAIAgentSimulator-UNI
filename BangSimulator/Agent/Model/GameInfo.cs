using System;
using System.Collections.Generic;
using System.Text;
using BangSimulator.Game;

namespace BangSimulator.Agent.Model
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
