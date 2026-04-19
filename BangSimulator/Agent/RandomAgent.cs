using System;
using System.Collections.Generic;
using System.Text;
using BangSimulator.Agent.Model;

namespace BangSimulator.Agent
{
    internal class RandomAgent : IAgent
    {
        private static Random random = new Random(); //TODO: add seed

        public void Reset()
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
