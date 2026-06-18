using System;
using System.Collections.Generic;
using System.Text;
using BangSimulatorLib.Agent;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorGui.Other
{
    public class StepAgent : IAgent
    {

        private IAgent agent;

        public StepAgent(IAgent agent)
        {
            this.agent = agent;
        }

        public void GameOver(PlayerRole winingRole)
        {
            this.agent.GameOver(winingRole);
        }

        public bool HasReward()
        {
            return this.agent.HasReward();
        }

        public void Reset()
        {
            this.agent.Reset();
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            //TODO:: WAIT HERE 

            return this.agent.Step(gameInfo);
        }

        public void SetEval(bool eval)
        {
            agent.SetEval(eval);
        }

        public List<float> GetRewards()
        {
            return agent.GetRewards();
        }

        public void Save(string pathFolder)
        {
            agent.Save(pathFolder);
        }

        public void Load(string path)
        {
            agent.Load(path);
        }
    }
}
