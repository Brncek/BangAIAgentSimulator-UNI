using System;
using System.Collections.Generic;
using System.Text;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent
{
    public class RandomAgent : IAgent
    {
        private static Random random = GlobalRnd.Rnd;

        public void GameOver(PlayerRole winingRole)
        {
        }


        public List<float> GetRewards()
        {
            return [];
        }

        public bool HasReward() => false;

        public void Load(string path)
        {
        }

        public void Reset()
        {
        }

        public void SetAutoSavePath(string pathFolder)
        {
        }

        public void SetEval(bool eval)
        {
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            var randomAction = gameInfo.AvanableActions[random.Next(gameInfo.AvanableActions.Count)];

            var action = new AgentAction
            {
                PlayedCard = randomAction.PlayedCard,
                target = randomAction.PotencialTargets.Length > 0 ? randomAction.PotencialTargets[random.Next(randomAction.PotencialTargets.Length)] : -1
            };

            return action;
        }
    }
}
