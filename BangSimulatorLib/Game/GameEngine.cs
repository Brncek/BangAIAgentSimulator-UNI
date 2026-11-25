using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using BangSimulatorLib.Agent;
using BangSimulatorLib.Agent.Model;
using Action = BangSimulatorLib.Agent.Model.Action;

namespace BangSimulatorLib.Game
{


    public class GameEngine
    {
        public Player[] Players { get; set; } = [];
        public Deck Deck { get; set; }
        private Player[] previousSeating = [];

        public GameEngine(Player[] players)
        {
            Players = players;

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].Id = i;
            }

            Deck = new Deck(players.Length);
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
                        new Player(PlayerRole.Bandit, agents[1]),
                        new Player(PlayerRole.Renegade, agents[2]),
                        new Player(PlayerRole.Bandit, agents[3])
                    };
                    break;
                case 5:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Bandit, agents[1]),
                        new Player(PlayerRole.Bandit, agents[2]),
                        new Player(PlayerRole.Renegade, agents[3]),
                        new Player(PlayerRole.Deputy, agents[4])
                    };
                    break;
                case 6:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Bandit, agents[1]),
                        new Player(PlayerRole.Bandit, agents[2]),
                        new Player(PlayerRole.Renegade, agents[3]),
                        new Player(PlayerRole.Deputy, agents[4]),
                        new Player(PlayerRole.Deputy, agents[5])
                    };
                    break;
                case 7:
                    Players = new Player[]
                    {
                        new Player(PlayerRole.Sheriff, agents[0]),
                        new Player(PlayerRole.Bandit, agents[1]),
                        new Player(PlayerRole.Bandit, agents[2]),
                        new Player(PlayerRole.Bandit, agents[3]),
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

            Deck = new Deck(agents.Length);
        }

        public GameResoult Play()
        {

            // IDs index the roster and statistics. Only this game's table order changes.
            List<Player> alivePlayers = Players.ToList();
            if (alivePlayers.Count > 1)
            {
                do
                {
                    for (int seat = alivePlayers.Count - 1; seat > 0; seat--)
                    {
                        int other = GlobalRnd.Rnd.Next(seat + 1);
                        (alivePlayers[seat], alivePlayers[other]) = (alivePlayers[other], alivePlayers[seat]);
                    }
                } while (alivePlayers.SequenceEqual(previousSeating));
            }
            previousSeating = alivePlayers.ToArray();
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


            int playerIndex = alivePlayers.FindIndex(p => p.Role == PlayerRole.Sheriff);

            if (playerIndex == -1)
            {
                playerIndex = 0;
            }

            int turn = 0;
            List<int[]> lives = [];

            while (true)
            {
                lives.Add(Players.Select(p => p.LifePoints).ToArray());
                turn++;

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
                        ApplyDamage(player, 3, null, dinamiteCard, alivePlayers, alivePlayers.Count);
                        skipTurn |= !alivePlayers.Contains(player);
                    }
                    else
                    {
                        int nextPlayerIndex = (playerIndex + 1) % alivePlayers.Count;
                        alivePlayers[nextPlayerIndex].CardsInPlay.Add(dinamiteCard);
                    }
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

                        var gameInfo = CreateInfo(player, alivePlayers,
                            BuildActions(playerIndex, alivePlayers, bangNotUsed));

                        playedAction = player.Agent.Step(gameInfo);

                        result = PlayAction(playerIndex, playedAction, alivePlayers, ref bangNotUsed);

                        if (result != null)
                            break;
                    }
                }
                else
                {
                    result = CheckForWin(alivePlayers);
                }


                if (result != null)
                {
                    lives.Add(Players.Select(p => p.LifePoints).ToArray());

                    result.LastRoundmemory = Deck.DeckMemory.ToArray();
                    result.Turns = turn;
                    result.LivesData = lives;
                    result.PlayerToPlayerBang = (int[,])Deck.PlayerToPlayerBang.Clone();

                    foreach (var item in Players)
                    {
                        item.Agent.GameOver(result.WinningRole);
                    }

                    return result;
                }
                AdvanceToNextLivingPlayer(ref playerIndex, alivePlayers, player);
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
                    case CardBangType.Duel:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = alivePlayers.Select(p => p.Id).Where(id => id != alivePlayers[playerIndex].Id).ToArray()
                            });
                        }
                        break;

                    case CardBangType.CatBalou:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = alivePlayers
                                                     .Where(p => p.Hand.Count > 0 || p.CardsInPlay.Count > 0)
                                                     .Select(p => p.Id)
                                                     .Where(id => id != alivePlayers[playerIndex].Id).ToArray()
                            });
                        }
                        break;
                    case CardBangType.Jail:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = alivePlayers
                                                    .Where(p => p.Role != PlayerRole.Sheriff && !p.CardsInPlay.Any(c => c.Type == CardBangType.Jail) && p.Hand.Count > 0)
                                                    .Select(p => p.Id)
                                                    .Where(id => id != alivePlayers[playerIndex].Id).ToArray()
                            });
                        }
                        break;
                    case CardBangType.Dinamite:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = [alivePlayers[playerIndex].Id]
                            });
                        }
                        break;
                    case CardBangType.Panic:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = GetAllPlayersInRange(playerIndex, alivePlayers, 1, true)
                            });
                        }
                        break;
                    case CardBangType.Beer when alivePlayers.Count > 2 && player.LifePoints < player.MaxLifePoints:
                        actions.Add(new Action { PlayedCard = card, PotencialTargets = [-1] });
                        break;
                    case CardBangType.Missed or CardBangType.Beer:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = []
                            });
                        }
                        break;

                    default:
                        {
                            actions.Add(new Action
                            {
                                PlayedCard = card,
                                PotencialTargets = [-1]
                            });
                        }
                        break;
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
            else if (action.PlayedCard.Color != CardBangColor.Blue)
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
                    GetPlayerById(action.target).CardsInPlay.Add(action.PlayedCard);
                }

                return null;
            }

            var actor = alivePlayers[playerIndex];
            var cause = action.PlayedCard;
            switch (action.PlayedCard.Type)
            {
                case CardBangType.Beer:
                    if (alivePlayers.Count > 2 && actor.LifePoints < actor.MaxLifePoints)
                        actor.LifePoints++;
                    break;
                case CardBangType.Bang:
                    bangNotUsed = false;
                    var target = GetPlayerById(action.target);
                    if (!Defended(target, actor, cause, alivePlayers))
                        ApplyDamage(target, 1, actor, cause, alivePlayers, alivePlayers.Count);
                    return CheckForWin(alivePlayers);

                case CardBangType.CatBalou:
                    var victim = GetPlayerById(action.target);
                    int slot = GlobalRnd.Rnd.Next(victim.Hand.Count + victim.CardsInPlay.Count);
                    var pile = slot < victim.Hand.Count ? victim.Hand : victim.CardsInPlay;
                    if (pile == victim.CardsInPlay) slot -= victim.Hand.Count;
                    Deck.DiscardCard(pile[slot], -1, -1);
                    pile.RemoveAt(slot);
                    break;

                case CardBangType.Duel:
                    var responder = GetPlayerById(action.target);
                    var opponent = actor;
                    while (Defended(responder, opponent, cause, alivePlayers))
                        (responder, opponent) = (opponent, responder);
                    ApplyDamage(responder, 1, opponent, cause, alivePlayers, alivePlayers.Count);
                    return CheckForWin(alivePlayers);

                case CardBangType.Gatling:
                case CardBangType.Indians:
                    // All targets of one card use the original participant count for Beer.
                    var targets = alivePlayers.Skip(playerIndex + 1).Concat(alivePlayers.Take(playerIndex)).ToArray();
                    int participants = alivePlayers.Count;
                    foreach (var other in targets)
                    {
                        if (!Defended(other, actor, cause, alivePlayers))
                            ApplyDamage(other, 1, actor, cause, alivePlayers, participants);
                    }
                    return CheckForWin(alivePlayers);

                case CardBangType.GeneralStore:
                    var market = alivePlayers.Select(_ => Deck.DrawCard()).ToList();
                    foreach (var buyer in alivePlayers.Skip(playerIndex).Concat(alivePlayers.Take(playerIndex)))
                    {
                        var offers = market.Select(c => new Action { PlayedCard = c, PotencialTargets = [-1] }).ToList();
                        var pick = buyer.Agent.Step(CreateInfo(buyer, alivePlayers, offers, cause, actor,
                            DecisionKind.StorePick));
                        if (pick?.PlayedCard == null || !market.Remove(pick.PlayedCard))
                            throw new InvalidOperationException("General Store requires an offered card.");
                        buyer.Hand.Add(pick.PlayedCard);
                    }
                    break;
                case CardBangType.Panic:
                    {
                        var targetP = GetPlayerById(action.target);
                        var randomCard = targetP.Hand[GlobalRnd.Rnd.Next(targetP.Hand.Count)];

                        targetP.Hand.Remove(randomCard);
                        alivePlayers[playerIndex].Hand.Add(randomCard);

                    }
                    break;
                case CardBangType.Salon:
                    {
                        alivePlayers.ForEach(p =>
                        {
                            if (p.LifePoints < p.MaxLifePoints)
                            {
                                p.LifePoints++;
                            }
                        });

                    }
                    break;
                case CardBangType.Stagecoach:
                    {
                        List<Card> cards = new List<Card>();
                        for (int i = 0; i < 2; i++)
                        {
                            cards.Add(Deck.DrawCard());
                        }

                        alivePlayers[playerIndex].Hand.AddRange(cards);

                    }
                    break;
                case CardBangType.WellsFargo:
                    {
                        List<Card> cards = new List<Card>();
                        for (int i = 0; i < 3; i++)
                        {
                            cards.Add(Deck.DrawCard());
                        }

                        alivePlayers[playerIndex].Hand.AddRange(cards);

                    }
                    break;
            }

            return null;
        }

        private GameInfo CreateInfo(Player player, List<Player> alive, List<Action> actions,
            Card? cause = null, Player? source = null, DecisionKind decision = DecisionKind.Turn)
        {
            return new GameInfo
            {
                PlayerRole = player.Role,
                PlayerHelth = player.LifePoints,
                GamePlayerLifes = Players.Select(p => p.LifePoints).ToArray(),
                DeadPlayerRoles = Players.Select(p => alive.Contains(p) ? (PlayerRole?)null : p.Role).ToArray(),
                ScherifId = Players.First(p => p.Role == PlayerRole.Sheriff).Id,
                CardsOut = [.. player.CardsInPlay],
                DeckMemory = Deck.DeckMemory.ToArray(),
                AvanableActions = actions,
                CardFromId = source?.Id ?? -1,
                ReactingToCard = cause,
                Decision = decision
            };
        }

        private Card? ChooseResponse(Player player, Player? source, Card cause, List<Player> alive,
            DecisionKind decision, IEnumerable<Card> options, int target)
        {
            var actions = options.Select(c => new Action { PlayedCard = c, PotencialTargets = [target] }).ToList();
            actions.Add(new Action { PlayedCard = null, PotencialTargets = [-1] });
            var reply = player.Agent.Step(CreateInfo(player, alive, actions, cause, source, decision));
            return reply?.PlayedCard != null && reply.target == target &&
                actions.Any(a => a.PlayedCard == reply.PlayedCard) ? reply.PlayedCard : null;
        }

        private bool Defended(Player player, Player source, Card cause, List<Player> alive)
        {
            bool dodge = cause.Type is CardBangType.Bang or CardBangType.Gatling;
            if (dodge && player.HasBarrel())
            {
                var drawn = Deck.DrawCard();
                Deck.DiscardCard(drawn, -1, -1);
                if (drawn.Suit == CardSuit.Hearts) return true;
            }
            var answer = dodge ? CardBangType.Missed : CardBangType.Bang;
            var choice = ChooseResponse(player, source, cause, alive, DecisionKind.Defend,
                player.Hand.Where(c => c.Type == answer), source.Id);
            if (choice == null) return false;
            player.Hand.Remove(choice);
            Deck.DiscardCard(choice, player.Id, -1);
            return true;
        }

        private void ApplyDamage(Player player, int damage, Player? killer, Card cause,
            List<Player> alive, int participants)
        {
            player.LifePoints -= damage;
            while (player.LifePoints <= 0 && participants > 2)
            {
                var beer = ChooseResponse(player, killer, cause, alive, DecisionKind.SaveLife,
                    player.Hand.Where(c => c.Type == CardBangType.Beer), -1);
                if (beer == null) break;
                player.Hand.Remove(beer);
                Deck.DiscardCard(beer, player.Id, -1);
                player.LifePoints++;
            }
            if (player.LifePoints > 0) return;
            player.LifePoints = 0;
            alive.Remove(player);
            DiscardAll(player);
            if (killer == null) return;
            if (player.Role == PlayerRole.Bandit)
            {
                for (int i = 0; i < 3; i++) killer.Hand.Add(Deck.DrawCard());
            }
            else if (killer.Role == PlayerRole.Sheriff && player.Role == PlayerRole.Deputy)
                DiscardAll(killer);
        }

        private static void AdvanceToNextLivingPlayer(ref int playerIndex, List<Player> alivePlayers, Player player)
        {
            int currentIndex = alivePlayers.FindIndex(candidate => candidate.Id == player.Id);
            playerIndex = currentIndex >= 0
                ? (currentIndex + 1) % alivePlayers.Count
                : playerIndex % alivePlayers.Count;
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
                else if (result.Contains(alivePlayers[targetIndex].Id))
                    break;
                else if (hasCards && alivePlayers[targetIndex].Hand.Count == 0)
                    continue;

                result.Add(alivePlayers[targetIndex].Id);
            }

            return result.ToArray();
        }

        private void DiscardAll(Player player)
        {
            player.Hand.ForEach(c => Deck.DiscardCard(c, -1, -1));
            player.CardsInPlay.ForEach(c => Deck.DiscardCard(c, -1, -1));
            player.Hand.Clear();
            player.CardsInPlay.Clear();
        }

        private GameResoult? CheckForWin(List<Player> alivePlayers)
        {
            List<Player> bandits = [];
            List<Player> deputys = [];
            Player? scheriff = null;
            Player? renegad = null;

            for (int i = 0; i < alivePlayers.Count; i++)
            {
                switch (alivePlayers[i].Role)
                {
                    case PlayerRole.Bandit:
                        bandits.Add(alivePlayers[i]);
                        break;
                    case PlayerRole.Renegade:
                        renegad = alivePlayers[i];
                        break;
                    case PlayerRole.Deputy:
                        deputys.Add(alivePlayers[i]);
                        break;
                    case PlayerRole.Sheriff:
                        scheriff = alivePlayers[i];
                        break;
                }
            }

            // A dead sheriff always ends the game.  The renegade wins only when
            // they are the sole survivor; in every other case the bandit side
            // wins.  This also prevents a deputy-only game from continuing.
            if (scheriff == null)
            {
                if (bandits.Count == 0 && deputys.Count == 0 && renegad != null)
                {
                    return new GameResoult
                    {
                        WinningRole = PlayerRole.Renegade,
                        WinningPlayers = [renegad]
                    };
                }

                return new GameResoult
                {
                    WinningRole = PlayerRole.Bandit,
                    WinningPlayers = [.. bandits]
                };
            }
            else if (scheriff != null && bandits.Count == 0 && renegad == null)
            {
                return new GameResoult
                {
                    WinningRole = PlayerRole.Sheriff,
                    WinningPlayers = [scheriff!, .. deputys]
                };
            }
            return null;
        }

        private Player GetPlayerById(int id)
        {
            return Players.First(p => p.Id == id);
        }
    }
}
