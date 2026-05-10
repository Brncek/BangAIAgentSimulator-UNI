using System;
using System.Collections.Generic;
using System.Text;
using BangSimulator.Game;

namespace BangSimulator.Agent.Model
{
    public class GameInfo
    {
        public int[] GamePlayerLifes { get; set; } = [];

        public int ScherifId { get; set; }

        public int PlayerHelth { get; set; } = 0;

        public PlayerRole PlayerRole { get; set; } 

        public List<Action> AvanableActions {  get; set; } = []; 

        public List<Card> CardsOut { get; set; } = [];

        public Card? ReactionTo  { get; set; } = null;

        public DeckMemory[] DeckMemory { get; set; } = [];

        public GameState GameState { get; set; } = GameState.InPlay;
    }

    public enum GameState
    {
        InPlay,
        Dead,
        Win,
        Defeat
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
