using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using BangSimulator.Agent;
using BangSimulator.Agent.Model;
using Action = BangSimulator.Agent.Model.Action;

namespace BangSimulator.Game
{


    public class GameEngine
    {

        public Player[] Players { get; set; } = [];
        public Deck Deck { get; set; } = new Deck(); 

        public GameEngine(Player[] players)
        {
            if (players.Length < 4 || players.Length > 7)
            {
                throw new ArgumentException("Number of players must be between 4 and 7.");
            }

            Players = players;
        }

        public GameEngine(IAgent[] agents)
        {
            if (agents.Length < 4 || agents.Length > 7)
            {
                throw new ArgumentException("Number of agents must be between 4 and 7.");
            }

            switch (agents.Length)
            {
                case 4:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Outlaw, agents[1]),
                        new Player(PlayerRole.Renegade, agents[2]),
                        new Player(PlayerRole.Outlaw, agents[3])
                    };
                    break;
                case 5:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Outlaw, agents[1]),
                        new Player(PlayerRole.Outlaw, agents[2]),
                        new Player(PlayerRole.Renegade, agents[3]),
                        new Player(PlayerRole.Deputy, agents[4])
                    };
                    break;
                case 6:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Outlaw, agents[1]),
                        new Player(PlayerRole.Outlaw, agents[2]),
                        new Player(PlayerRole.Renegade, agents[3]),
                        new Player(PlayerRole.Deputy, agents[4]),
                        new Player(PlayerRole.Deputy, agents[5])
                    };
                    break;
                case 7:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Outlaw, agents[1]),
                        new Player(PlayerRole.Outlaw, agents[2]),
                        new Player(PlayerRole.Outlaw, agents[3]),
                        new Player(PlayerRole.Renegade, agents[4]),
                        new Player(PlayerRole.Deputy, agents[5]),
                        new Player(PlayerRole.Deputy, agents[6])
                    };
                    break;
            }
        }
    
    
        public GameResoult Play()
        {
            List<Player> alivePlayers = Players.ToList();
            alivePlayers.ForEach(p => p.Reset());
            Deck.Reset();
            
            int ScheriffIndex = alivePlayers.FindIndex(p => p.Role == PlayerRole.Sheriff);

            int playerIndex = 0;

            while (true)
            {

                bool skipTurn = false;

                var player = alivePlayers[playerIndex];
                var jailCard = player.HasJail();
                var dinamiteCard = player.HasDinamite();

                if (jailCard != null)
                {
                    var card = Deck.DrawCard();

                    skipTurn = card.Suit != CardSuit.Hearts;
                    Deck.DiscardCard(card, -1);
                    Deck.DiscardCard(jailCard, -1);
                }

                if (dinamiteCard != null)
                {
                    var card = Deck.DrawCard();
                    bool explode = card.Suit == CardSuit.Spades && card.Value >= CardValue.Two && card.Value <= CardValue.Nine;
                    Deck.DiscardCard(card, -1);

                    if (explode)
                    {
                        Deck.DiscardCard(dinamiteCard, -1);
                        player.LifePoints -= 3;
                        if (player.LifePoints <= 0)
                        {
                            var beer = player.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);

                            if (beer != null)
                            {
                                player.Hand.Remove(beer);
                                Deck.DiscardCard(beer, playerIndex);
                                player.LifePoints = 1;
                            }
                            else
                            {
                                player.LifePoints = 0;
                                alivePlayers.RemoveAt(playerIndex);
                                PlayerDied(player);
                                skipTurn = true;
                            }
                        }
                    }
                    else
                    {
                        int nextPlayerIndex = (playerIndex + 1) % alivePlayers.Count;
                        alivePlayers[nextPlayerIndex].CardsInPlay.Add(dinamiteCard);
                    }
                }

                //use beer if player is damaged
                while (player.LifePoints < player.MaxLifePoints && player.Hand.Any(c => c.Type== CardBangType.Beer))
                {
                    var beer = player.Hand.First(c => c.Type == CardBangType.Beer);
                    player.Hand.Remove(beer);
                    Deck.DiscardCard(beer, playerIndex);
                    player.LifePoints += 1;
                }


                GameResoult? result = null;

                //agent turns
                if (!skipTurn)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var card = Deck.DrawCard();
                        player.Hand.Add(card);
                    }

                    AgentAction? playedAction = null;

                    bool bangNotUsed = true;

                    while (playedAction == null || playedAction.PlayedCard != null)
                    {
                        GameInfo gameInfo = new();
                        gameInfo.PlayerHelth = player.LifePoints;
                        gameInfo.PlayerRole = player.Role;
                        gameInfo.GamePlayerLifes = Players.Select(p => p.LifePoints).ToArray();
                        gameInfo.ScherifIndex = ScheriffIndex;
                        gameInfo.DeckMemory = Deck.DeckMemory.ToArray();
                        gameInfo.CardsOut = player.CardsInPlay;
                        gameInfo.GameState = GameState.InPlay;

                        gameInfo.AvanableActions = BuildActions(playerIndex, alivePlayers, bangNotUsed);

                        playedAction = player.Agent.Step(gameInfo);

                        //TODO: play action


                        result = CheckForWin(alivePlayers);
                        if ( result != null)
                            break;
                    }
                }
                else
                {
                    result = CheckForWin(alivePlayers);
                }

                playerIndex++;
                playerIndex = playerIndex % alivePlayers.Count;
            
                if (result != null)
                {
                    return result;
                }
            }
        }
        
        private List<Action> BuildActions(int playerIndex, List<Player> alivePlayers, bool bangNotUsed)
        {

            var player = alivePlayers[playerIndex];
            var actions = new List<Action>();

            foreach (var card in player.Hand)
            {
                
                switch (card.Type)
                {
                    case CardBangType.Bang:
                        {
                            if (bangNotUsed || player.InfiniteBangs())
                            {
                                actions.Add(new Action
                                {
                                    PlayedCard = card,
                                    PotencialTargets = GetAllPlayersInRange(playerIndex, alivePlayers, player.GetRange())
                                });
                            }
                            else
                            {
                                actions.Add(new Action
                                {
                                    PlayedCard = card,
                                    PotencialTargets = []
                                });
                            }
                        }
                        break;
                    case CardBangType.CatBalou or CardBangType.Duel or CardBangType.Dinamite or CardBangType.Jail:
                        { 
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = GetAllPlayersButMe(playerIndex, alivePlayers)
                            });
                        } break; 
                    case CardBangType.Panic:
                        { 
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = GetAllPlayersInRange(playerIndex, alivePlayers, 1)
                            });
                        } break; 
                    case CardBangType.Missed or CardBangType.Beer:
                        { 
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = []
                            });
                        } break; 

                    default:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = [-1]
                            });
                        } break;
                }
                
            }

            foreach (var action in actions)
            {
                action.PotencialTargets = [.. action.PotencialTargets, -2];
            }


            //END TURN ACTION
            if (player.LifePoints >= player.Hand.Count)
            {
                actions.Add(new Action
                {
                    PlayedCard = null,
                    PotencialTargets = [-1]
                });
            }

            return actions;
        }

        private int[] GetAllPlayersButMe(int playerIndex, List<Player> alivePlayers)
        {
            return alivePlayers.Select((p, i) => i).Where(i => i != playerIndex).ToArray();
        }

        private int[] GetAllPlayersInRange(int playerIndex, List<Player> alivePlayers, int range)
        {
            List<int> result = new List<int>();

            //TO RIGHT
            for (int i = 1; i <= range; i++)
            {
                int targetIndex = (playerIndex + i) % alivePlayers.Count;

                if (targetIndex == playerIndex)
                    break;
                else if (alivePlayers[targetIndex].HasMustang() && i == range)
                    break;
                
                result.Add(targetIndex);
            }

            //TO LEFT
            for (int i = 1; i <= range; i++)
            {
                int targetIndex = (playerIndex - i + alivePlayers.Count) % alivePlayers.Count;

                if (targetIndex == playerIndex)
                    break;
                else if (alivePlayers[targetIndex].HasMustang() && i == range)
                    break;
                else if (result.Contains(targetIndex))
                    break;

                result.Add(targetIndex);
            }

            return result.ToArray();
        }

        private void PlayerDied(Player player)
        {
            player.Hand.ForEach(c => Deck.DiscardCard(c, -1));
            player.CardsInPlay.ForEach(c => Deck.DiscardCard(c, -1));
        }

        private GameResoult? CheckForWin(List<Player> alivePlayers)
        {
            Player[] bandits = alivePlayers.Where(p => p.Role == PlayerRole.Outlaw).ToArray();
            Player[] deputys = alivePlayers.Where(p => p.Role == PlayerRole.Deputy).ToArray();
            Player[] scheriff = alivePlayers.Where(p => p.Role == PlayerRole.Sheriff).ToArray();
            Player[] renegad = alivePlayers.Where(p => p.Role == PlayerRole.Renegade).ToArray();

            if (scheriff.Length > 0)
            {
                if (bandits.Length > 0)
                {
                    return new GameResoult
                    {
                        WinningRole = PlayerRole.Outlaw,
                        WinningPlayers = Players.Where(p => p.Role == PlayerRole.Outlaw).ToArray()
                    };
                }
                else if (renegad.Length > 0 && deputys.Length == 0)
                {
                    return new GameResoult
                    {
                        WinningRole = PlayerRole.Renegade,
                        WinningPlayers = Players.Where(p => p.Role == PlayerRole.Renegade).ToArray()
                    };
                }
                else
                    return null;
            }
            else if (bandits.Length == 0)
            {
                return new GameResoult
                {
                    WinningRole = PlayerRole.Sheriff,
                    WinningPlayers = Players.Where(p => p.Role == PlayerRole.Sheriff || p.Role == PlayerRole.Deputy).ToArray()
                };
            }

            return null;
        }
    }

    public class GameResoult
    {
        public PlayerRole WinningRole { get; set; }
        public Player[] WinningPlayers { get; set; } = [];
    }
}
