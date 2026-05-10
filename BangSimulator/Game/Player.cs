using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using BangSimulator.Agent;
using BangSimulator.Agent.Model;

namespace BangSimulator.Game
{
    public class Player
    {
        public int Id { get; set; }

        public List<Card> Hand { get; set; } = [];
        public List<Card> CardsInPlay { get; set; } = [];
        public int LifePoints { get; set; } = 0;
        public int MaxLifePoints { get; set; } = 0;
        public PlayerRole Role { get; set; }
        public IAgent Agent { get; set; }

        public Player(PlayerRole role, IAgent agent)
        {
            Role = role;
            Agent = agent;
            Reset();
        }

        public int GetRange()
        {
            int range = 1; // default range is 1
            foreach (var card in CardsInPlay)
            {
                if (card.Type == CardBangType.Remington)
                {
                    range += 2;
                }
                else if (card.Type == CardBangType.Carabine)
                {
                    range += 3;
                }
                else if (card.Type == CardBangType.Schofield)
                {
                    range += 1;
                }
                else if (card.Type == CardBangType.scope)
                {
                    range += 1;
                }
                else if (card.Type == CardBangType.Winchester)
                {
                    range += 4;
                }
            }
            return range;
        }

        public bool InfiniteBangs()
        {
            return CardsInPlay.Any(c => c.Type == CardBangType.Vulcanic);
        }

        public bool HasMustang()
        {
            return CardsInPlay.Any(c => c.Type == CardBangType.Mustang);
        }

        public bool HasBarrel()
        {
            return CardsInPlay.Any(c => c.Type == CardBangType.Barrel);
        }

        public Card? HasJail()
        {
            var card = CardsInPlay.FirstOrDefault(c => c.Type == CardBangType.Jail);
            if (card == null)
            {
                return null;
            }
            else
            {
                CardsInPlay.Remove(card);
                return card;
            }
        }

        public Card? HasDinamite()
        {
            var card = CardsInPlay.FirstOrDefault(c => c.Type == CardBangType.Dinamite);
            if (card == null)
            {
                return null;
            }
            else
            {
                CardsInPlay.Remove(card);
                return card;
            }
        }

        public void Reset()
        {
            if (Role == PlayerRole.Sheriff)
            {
                MaxLifePoints = 5;
            }
            else
            {
                MaxLifePoints = 4;
            }

            LifePoints = MaxLifePoints;
            
            Agent.Reset();
        }
    } 


    public enum PlayerRole
    {
        Sheriff,
        Deputy,
        Outlaw,
        Renegade
    }
}
