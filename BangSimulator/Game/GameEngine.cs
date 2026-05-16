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

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].Id = i;
            }
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

            for (int i = 0; i < GlobalRnd.Rnd.Next(50); i++)
            {
                var rndIndex1 = GlobalRnd.Rnd.Next(Players.Length);
                var rndIndex2 = GlobalRnd.Rnd.Next(Players.Length);

                if (rndIndex1 != rndIndex2)
                {
                    (Players[rndIndex1], Players[rndIndex2]) = (Players[rndIndex2], Players[rndIndex1]);
                }
            }

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].Id = i;
            }
        }
    
        public GameResoult Play()
        {
            List<Player> alivePlayers = Players.ToList();
            alivePlayers.ForEach(p => p.Reset());
            Deck.Reset();

            alivePlayers.ForEach(p =>
            {
                for (int i = 0; i < p.LifePoints; i++)
                {
                    var card = Deck.DrawCard();
                    p.Hand.Add(card);
                }
            });

            int playerIndex = 0;

            var scherifID = alivePlayers.Find(p => p.Role == PlayerRole.Sheriff)!.Id;

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
                    Deck.DiscardCard(card, -1, -1);
                    Deck.DiscardCard(jailCard, -1, -1);
                }

                if (dinamiteCard != null)
                {
                    var card = Deck.DrawCard();
                    bool explode = card.Suit == CardSuit.Spades && card.Value >= CardValue.Two && card.Value <= CardValue.Nine;
                    Deck.DiscardCard(card, -1, -1);

                    if (explode)
                    {
                        Deck.DiscardCard(dinamiteCard, -1, -1);
                        player.LifePoints -= 3;
                        if (player.LifePoints <= 0)
                        {
                            var beer = player.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);

                            if (beer != null)
                            {
                                player.Hand.Remove(beer);
                                Deck.DiscardCard(beer, player.Id, -1);
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
                    Deck.DiscardCard(beer, player.Id, -1);
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

                    while ((playedAction == null || playedAction.PlayedCard != null) && player.LifePoints > 0)
                    {
                        playerIndex = alivePlayers.FindIndex(p => p.Id == player.Id);

                        GameInfo gameInfo = new();
                        gameInfo.PlayerHelth = player.LifePoints;
                        gameInfo.PlayerRole = player.Role;
                        gameInfo.GamePlayerLifes = Players.Select(p => p.LifePoints).ToArray();
                        gameInfo.ScherifId = scherifID;
                        gameInfo.DeckMemory = Deck.DeckMemory.ToArray();
                        gameInfo.CardsOut = player.CardsInPlay;

                        gameInfo.AvanableActions = BuildActions(playerIndex, alivePlayers, bangNotUsed);

                        playedAction = player.Agent.Step(gameInfo);

                        result = PlayAction(playerIndex, playedAction, alivePlayers, ref bangNotUsed);
                        
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
                            var action = new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = GetAllPlayersButMe(playerIndex, alivePlayers, true)
                            });
                        } break; 
                    case CardBangType.Panic:
                        { 
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = GetAllPlayersInRange(playerIndex, alivePlayers, 1, true)
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

        private GameResoult? PlayAction(int playerIndex, AgentAction action, List<Player> alivePlayers, ref bool bangNotUsed)
        {
            if (action.PlayedCard == null)
            {
                return null;
            }

            alivePlayers[playerIndex].Hand.Remove(action.PlayedCard);
            
            if (action.target == -2)
            {
                Deck.DiscardCard(action.PlayedCard, -1, -1);
                return null;
            }
            else
            {
                Deck.DiscardCard(action.PlayedCard, alivePlayers[playerIndex].Id, action.target);
            }

            if (action.PlayedCard.Color == CardBangColor.Blue)
            {
                if (action.PlayedCard.IsGun())
                {
                    var potencialGun = alivePlayers[playerIndex].CardsInPlay.FirstOrDefault(c => c.IsGun());
                    if (potencialGun != null)
                    {
                        Deck.DiscardCard(potencialGun, -1, -1);
                        alivePlayers[playerIndex].CardsInPlay.Remove(potencialGun);
                    }

                    alivePlayers[playerIndex].CardsInPlay.Add(action.PlayedCard);
                }
                else if (action.PlayedCard.Type == CardBangType.Barrel 
                    || action.PlayedCard.Type == CardBangType.scope
                    || action.PlayedCard.Type == CardBangType.Mustang)
                {
                    var potencialBlue = alivePlayers[playerIndex].CardsInPlay.FirstOrDefault(c => c.Type == action.PlayedCard.Type);
                    if (potencialBlue != null)
                    {
                        Deck.DiscardCard(potencialBlue, -1, -1);
                        alivePlayers[playerIndex].CardsInPlay.Remove(potencialBlue);
                    }

                    alivePlayers[playerIndex].CardsInPlay.Add(action.PlayedCard);

                }
                else if (action.PlayedCard.Type == CardBangType.Dinamite 
                    || action.PlayedCard.Type == CardBangType.Jail)
                {
                    var target = GetPlayerById(action.target);
                    target.CardsInPlay.Add(action.PlayedCard);

                }

                return null;
            }

            switch (action.PlayedCard.Type)
            {
                case CardBangType.Bang:
                    {
                        bangNotUsed = false;
                        var targetP = GetPlayerById(action.target);
                        if (targetP.HasBarrel())
                        {
                            var card = Deck.DrawCard();
                            bool missed = card.Suit == CardSuit.Hearts;
                            Deck.DiscardCard(card, -1, -1);
                            if (missed)
                            {
                                return null;
                            }
                        }

                        var missedCard = targetP.Hand.FirstOrDefault(c => c.Type == CardBangType.Missed);
                        if (missedCard != null)
                        {
                            targetP.Hand.Remove(missedCard);
                            Deck.DiscardCard(missedCard, action.target, -1);
                            return null;
                        }

                        targetP.LifePoints -= 1;

                        if (targetP.LifePoints <= 0)
                        {
                            var beer = targetP.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);
                            if (beer != null)
                            {
                                targetP.Hand.Remove(beer);
                                Deck.DiscardCard(beer, action.target, -1);
                                targetP.LifePoints = 1;
                            }
                            else
                            {
                                targetP.LifePoints = 0;
                                alivePlayers.Remove(targetP);
                                PlayerDied(targetP);

                                var result = CheckForWin(alivePlayers);
                                if (result != null)
                                {
                                    return result;
                                }
                            }
                        }

                    } break; 
                case CardBangType.CatBalou:
                    {
                        var targetP = GetPlayerById(action.target);
                        var randomCard = targetP.Hand[GlobalRnd.Rnd.Next(targetP.Hand.Count)];

                        targetP.Hand.Remove(randomCard);
                        Deck.DiscardCard(randomCard, -1, -1);

                    } break;
                case CardBangType.Duel:
                    {
                        var targetP = GetPlayerById(action.target);
                        var player = alivePlayers[playerIndex];

                        int playerBangCount = player.Hand.Count(c => c.Type == CardBangType.Bang);
                        int targetBangCount = targetP.Hand.Count(c => c.Type == CardBangType.Bang);

                        if (playerBangCount > targetBangCount)
                        {

                            for (int i = 0; i < targetBangCount + 1; i++)
                            {
                                var bangCard = targetP.Hand.FirstOrDefault(c => c.Type == CardBangType.Bang)!;
                                var playerBangCard = player.Hand.FirstOrDefault(c => c.Type == CardBangType.Bang)!;
                                
                                targetP.Hand.Remove(bangCard);
                                player.Hand.Remove(playerBangCard);
                                Deck.DiscardCard(bangCard, action.target, -1);
                                Deck.DiscardCard(playerBangCard, player.Id, -1);
                            }


                            targetP.LifePoints -= 1;
                            if (targetP.LifePoints <= 0)
                            {
                                var beer = targetP.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);
                                if (beer != null)
                                {
                                    targetP.Hand.Remove(beer);
                                    Deck.DiscardCard(beer, targetP.Id, -1);
                                    targetP.LifePoints = 1;
                                }
                                else
                                {
                                    targetP.LifePoints = 0;
                                    alivePlayers.Remove(targetP);
                                    PlayerDied(targetP);
                                    var result = CheckForWin(alivePlayers);
                                    if (result != null)
                                    {
                                        return result;
                                    }
                                }
                            }
                        }
                        else 
                        {
                            for (int i = 0; i < playerBangCount + 1; i++)
                            {
                                var bangCard = targetP.Hand.FirstOrDefault(c => c.Type == CardBangType.Bang)!;
                                var playerBangCard = player.Hand.FirstOrDefault(c => c.Type == CardBangType.Bang)!;

                                targetP.Hand.Remove(bangCard);
                                player.Hand.Remove(playerBangCard);
                                Deck.DiscardCard(bangCard, action.target, -1);
                                Deck.DiscardCard(playerBangCard, player.Id, -1);
                            }

                            player.LifePoints -= 1; 

                            if (player.LifePoints <= 0)
                            {
                                var beer = player.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);
                                if (beer != null)
                                {
                                    player.Hand.Remove(beer);
                                    Deck.DiscardCard(beer, player.Id, -1);
                                    player.LifePoints = 1;
                                }
                                else
                                {
                                    player.LifePoints = 0;
                                    alivePlayers.Remove(player);
                                    PlayerDied(player);
                                    var result = CheckForWin(alivePlayers);
                                    if (result != null)
                                    {
                                        return result;
                                    }
                                }
                            }
                        }


                    } break; 
                case CardBangType.Gatling:
                    {
                        List<Player> toBeRemoved = [];

                        alivePlayers.ForEach(p =>
                        {
                            var missedCard = p.Hand.FirstOrDefault(c => c.Type == CardBangType.Missed);

                            if (missedCard != null)
                            {
                                p.Hand.Remove(missedCard);
                                Deck.DiscardCard(missedCard, p.Id, -1);
                            }
                            else
                            {
                                p.LifePoints -= 1;
                                if (p.LifePoints <= 0)
                                {
                                    var beer = p.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);
                                    if (beer != null)
                                    {
                                        p.Hand.Remove(beer);
                                        Deck.DiscardCard(beer, p.Id, -1);
                                        p.LifePoints = 1;
                                    }
                                    else
                                    {
                                        p.LifePoints = 0;
                                        toBeRemoved.Add(p);
                                        PlayerDied(p);
                                        
                                    }
                                }
                            }
                        });

                        for (int i = toBeRemoved.Count - 1; i >= 0; i--)
                        {
                            alivePlayers.Remove(toBeRemoved[i]);
                            var result = CheckForWin(alivePlayers);
                            if (result != null)
                            {
                                return result;
                            }
                        }

                    } break; 
                case CardBangType.GeneralStore:
                    { 
                        alivePlayers.ForEach(p =>
                        {
                            var card = Deck.DrawCard();
                            p.Hand.Add(card);
                        });

                    } break; 
                case CardBangType.Indians:
                    {
                        List<Player> toBeRemoved = [];

                        alivePlayers.ForEach(p =>
                        {
                            var bangCard = p.Hand.FirstOrDefault(c => c.Type == CardBangType.Bang);

                            if (bangCard != null)
                            {
                                p.Hand.Remove(bangCard);
                                Deck.DiscardCard(bangCard, p.Id, -1);
                            }
                            else
                            {
                                p.LifePoints -= 1;
                                if (p.LifePoints <= 0)
                                {
                                    var beer = p.Hand.FirstOrDefault(c => c.Type == CardBangType.Beer);
                                    if (beer != null)
                                    {
                                        p.Hand.Remove(beer);
                                        Deck.DiscardCard(beer, p.Id, -1);
                                        p.LifePoints = 1;
                                    }
                                    else
                                    {
                                        p.LifePoints = 0;
                                        toBeRemoved.Add(p);
                                        PlayerDied(p);
                                    }
                                }
                            }
                        });

                        for (int i = toBeRemoved.Count - 1; i >= 0; i--)
                        {
                            alivePlayers.Remove(toBeRemoved[i]);
                            var result = CheckForWin(alivePlayers);
                            if (result != null)
                            {
                                return result;
                            }
                        }

                    } break; 
                case CardBangType.Panic:
                    {
                        var targetP = GetPlayerById(action.target);
                        var randomCard = targetP.Hand[GlobalRnd.Rnd.Next(targetP.Hand.Count)];
                        
                        targetP.Hand.Remove(randomCard);
                        alivePlayers[playerIndex].Hand.Add(randomCard);

                    } break;
                case CardBangType.Salon:
                    {
                        alivePlayers.ForEach(p =>
                        {
                            if (p.LifePoints < p.MaxLifePoints)
                            {
                                p.LifePoints++;
                            }
                        });

                    } break;
                case CardBangType.Stagecoach:
                    { 
                        List<Card> cards = new List<Card>();
                        for (int i = 0; i < 2; i++)
                        {
                            cards.Add(Deck.DrawCard());
                        }

                        alivePlayers[playerIndex].Hand.AddRange(cards);

                    } break;
                case CardBangType.WellsFargo:
                    {
                        List<Card> cards = new List<Card>();
                        for (int i = 0; i < 3; i++)
                        {
                            cards.Add(Deck.DrawCard());
                        }

                        alivePlayers[playerIndex].Hand.AddRange(cards);

                    } break;
            }

            return null;
        }

        private int[] GetAllPlayersButMe(int playerIndex, List<Player> alivePlayers, bool hasCards = false)
        {
            if (hasCards)
            {
                return alivePlayers.Where(p => p.Hand.Count > 0).Select((p, i) => p.Id).Where(id => id != alivePlayers[playerIndex].Id).ToArray();
            }

            return alivePlayers.Select((p, i) => p.Id).Where(id => id != alivePlayers[playerIndex].Id).ToArray();
        }

        private int[] GetAllPlayersInRange(int playerIndex, List<Player> alivePlayers, int range, bool hasCards = false)
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
                else if (hasCards && alivePlayers[targetIndex].Hand.Count == 0)
                    continue;
                
                result.Add(alivePlayers[targetIndex].Id);
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
                else if (hasCards && alivePlayers[targetIndex].Hand.Count == 0)
                    continue;

                result.Add(alivePlayers[targetIndex].Id);
            }

            return result.ToArray();
        }

        private void PlayerDied(Player player)
        {
            player.Hand.ForEach(c => Deck.DiscardCard(c, -1, -1));
            player.CardsInPlay.ForEach(c => Deck.DiscardCard(c, -1, -1));
        }

        private GameResoult? CheckForWin(List<Player> alivePlayers)
        {
            Player[] bandits = alivePlayers.Where(p => p.Role == PlayerRole.Outlaw).ToArray();
            Player[] deputys = alivePlayers.Where(p => p.Role == PlayerRole.Deputy).ToArray();
            Player[] scheriff = alivePlayers.Where(p => p.Role == PlayerRole.Sheriff).ToArray();
            Player[] renegad = alivePlayers.Where(p => p.Role == PlayerRole.Renegade).ToArray();

            if (scheriff.Length == 0 && bandits.Length > 0)
            {
                return new GameResoult
                {
                    WinningRole = PlayerRole.Outlaw,
                    WinningPlayers = Players.Where(p => p.Role == PlayerRole.Outlaw).ToArray()
                };
                
            }
            else if (bandits.Length == 0 && renegad.Length == 0)
            {
                return new GameResoult
                {
                    WinningRole = PlayerRole.Sheriff,
                    WinningPlayers = Players.Where(p => p.Role == PlayerRole.Sheriff || p.Role == PlayerRole.Deputy).ToArray()
                };
            }
            else if (scheriff.Length == 0 && bandits.Length == 0 && deputys.Length == 0 && renegad.Length == 1)
            {
                return new GameResoult
                {
                    WinningRole = PlayerRole.Renegade,
                    WinningPlayers = Players.Where(p => p.Role == PlayerRole.Renegade).ToArray()
                };
            }

            return null;
        }
    
        private Player GetPlayerById(int id)
        {
            return Players.First(p => p.Id == id);
        }
    }

    public class GameResoult
    {
        public PlayerRole WinningRole { get; set; }
        public Player[] WinningPlayers { get; set; } = [];

        public override string ToString()
        {
            return $"Winning Role: {WinningRole}, Winning Players: {string.Join(", ", WinningPlayers.Select(p => p.Id))}";
        }
    }
}
