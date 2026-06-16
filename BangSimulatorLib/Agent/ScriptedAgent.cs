using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;
using Action = BangSimulatorLib.Agent.Model.Action;

namespace BangSimulatorLib.Agent
{
    public class ScriptedAgent : IAgent
    {
        public void GameOver(PlayerRole winingRole)
        {
        }

        public void Reset()
        {
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            switch (gameInfo.PlayerRole)
            {
                case PlayerRole.Sheriff:
                    return SheriffAction(gameInfo);
                case PlayerRole.Bandit:
                    return BanditAction(gameInfo);
                case PlayerRole.Deputy:
                    return DeputyAction(gameInfo);
                default: // PlayerRole.Renegade
                    return RenegadeAction(gameInfo);
            }
        }

        private AgentAction SheriffAction(GameInfo gameInfo)
        {
            var blueAction = PlayBestBlue(gameInfo);
            if (blueAction != null) return blueAction;

            var bangActions = gameInfo.AvanableActions.FindAll(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Bang);

            if (bangActions.Count > 0)
            {
                var targets = bangActions[0].PotencialTargets.Where(t => t != -2).ToArray();

                if (targets.Any())
                    return new AgentAction
                    {
                        PlayedCard = bangActions[0].PlayedCard,
                        target = targets[GlobalRnd.Rnd.Next(targets.Length)]
                    };
            }

            return PlayRandomAction(gameInfo);

            throw new NotImplementedException();
        }

        private AgentAction BanditAction(GameInfo gameInfo)
        {
            var blueAction = PlayBestBlue(gameInfo);
            if (blueAction != null) return blueAction;

            var bangActions = gameInfo.AvanableActions.FindAll(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Bang);
            
            if (bangActions.Count > 0)
            {
                if (bangActions[0].PotencialTargets.Contains(gameInfo.ScherifId))
                {
                    return new AgentAction
                    {
                        PlayedCard = bangActions[0].PlayedCard,
                        target = gameInfo.ScherifId
                    };
                }
            }

            return PlayRandomAction(gameInfo);
        }

        private AgentAction DeputyAction(GameInfo gameInfo)
        {
            var blueAction = PlayBestBlue(gameInfo);
            if (blueAction != null) return blueAction;

            var bangActions = gameInfo.AvanableActions.FindAll(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Bang);
            
            if (bangActions.Count > 0) 
            {
                var targets = bangActions[0].PotencialTargets.Where(t => !(t == gameInfo.ScherifId || t == -2) ).ToArray();

                if (targets.Any())
                {
                    return new AgentAction
                    {
                        PlayedCard = bangActions[0].PlayedCard,
                        target = targets[GlobalRnd.Rnd.Next(targets.Length)]
                    };
                }
            }

            return PlayRandomAction(gameInfo);
            
        }

        private AgentAction RenegadeAction(GameInfo gameInfo)
        {
            var blueAction = PlayBestBlue(gameInfo);
            if (blueAction != null) return blueAction;

            var bangActions = gameInfo.AvanableActions.FindAll(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Bang);
            
            if (bangActions.Count > 0)
            {
                var targets = bangActions[0].PotencialTargets.Where(t => t != -2).ToArray();

                if (targets.Any())
                    return new AgentAction
                    {
                        PlayedCard = bangActions[0].PlayedCard,
                        target = targets[GlobalRnd.Rnd.Next(targets.Length)]
                    };
            }

            return PlayRandomAction(gameInfo);
        }

        private AgentAction? PlayBestBlue(GameInfo gameInfo)
        {
            var blueActions = gameInfo.AvanableActions.FindAll(a => a.PlayedCard != null && a.PlayedCard.Color == CardBangColor.Blue);

            if (blueActions.Count > 0)
            {
                var gunActions = blueActions.Where(a => a.PlayedCard != null && a.PlayedCard.IsGun()).ToList();
                
                if (gunActions.Count > 0 && gameInfo.CardsOut.Any(c => c.IsGun()))
                {
                    return new AgentAction
                    {
                        PlayedCard = gunActions[GlobalRnd.Rnd.Next(gunActions.Count)].PlayedCard,
                        target = -1
                    };
                }

                var barelActions = blueActions.Where(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Barrel).ToList();
                if (barelActions.Count > 0)
                {
                    return new AgentAction
                    {
                        PlayedCard = barelActions[GlobalRnd.Rnd.Next(barelActions.Count)].PlayedCard,
                        target = -1
                    };
                }

                var mustangActions = blueActions.Where(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Mustang).ToList();
                if (mustangActions.Count > 0)
                {
                    return new AgentAction
                    {
                        PlayedCard = mustangActions[GlobalRnd.Rnd.Next(mustangActions.Count)].PlayedCard,
                        target = -1
                    };
                }

                var scopeActions = blueActions.Where(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.scope).ToList();
                if (scopeActions.Count > 0)
                {
                    return new AgentAction
                    {
                        PlayedCard = scopeActions[GlobalRnd.Rnd.Next(scopeActions.Count)].PlayedCard,
                        target = -1
                    };
                }

                var dinamiteActions = blueActions.Where(a => a.PlayedCard != null && a.PlayedCard.Type == CardBangType.Dinamite).ToList();
                if (dinamiteActions.Count > 0)
                {
                    var targets = dinamiteActions[0].PotencialTargets.Where(t => t != -2).ToArray();

                    if (targets.Length > 0)
                    {
                        return new AgentAction
                        {
                            PlayedCard = dinamiteActions[GlobalRnd.Rnd.Next(dinamiteActions.Count)].PlayedCard,
                            target = targets[GlobalRnd.Rnd.Next(targets.Length)]
                        };
                    }
                }
            }

            return null;
        }

        private AgentAction PlayRandomAction(GameInfo gameInfo)
        {
            var randomAction = gameInfo.AvanableActions[GlobalRnd.Rnd.Next(gameInfo.AvanableActions.Count)];

            return new AgentAction
            {
                PlayedCard = randomAction.PlayedCard,
                target = randomAction.PotencialTargets[GlobalRnd.Rnd.Next(randomAction.PotencialTargets.Length)]
            };
        }

        public bool HasReward() => false;

        public double GetCumulativeReward()
        {
            return 0;
        }
    }
}
