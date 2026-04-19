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
                        alivePlayers[nextPlayerIndex].InPlay.Add(dinamiteCard);
                    }
                }


                if(!skipTurn)
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
                        gameInfo.CardsOut = player.InPlay;
                        gameInfo.AvanableActions = new List<Action>();
                        //TODO: build AvanableActions

                        playedAction = player.Agent.Step(gameInfo);

                        //TODO: play action
                    }
                }

                playerIndex++;
                playerIndex = playerIndex % alivePlayers.Count;
            
                var result = CheckForWin(alivePlayers);
                if (result != null)
                {
                    return result;
                }
            }
        }
        

        private void PlayerDied(Player player)
        {
            player.Hand.ForEach(c => Deck.DiscardCard(c, -1));
            player.InPlay.ForEach(c => Deck.DiscardCard(c, -1));
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
