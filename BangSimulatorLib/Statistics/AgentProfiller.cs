using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BangSimulatorLib.Agent;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Statistics
{
    internal class AgentProfiller : IAgent
    {
        private double time = 0;
        private int calls = 0;
        private IAgent agent;

        Stopwatch stopwatch = new Stopwatch();
        
        public AgentProfiller(IAgent agent)
        {
            if (agent is AgentProfiller)
            {
                throw new ArgumentException("Profiler agent is forbiden input");
            }

            this.agent = agent;
        }

        public void GameOver(PlayerRole winingRole)
        {
            agent.GameOver(winingRole);
        }

        public void Reset()
        {
            agent.Reset();
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            calls++;
            stopwatch.Start();
           
            var action = agent.Step(gameInfo);

            stopwatch.Stop();

            time += stopwatch.ElapsedMilliseconds;
        
            stopwatch.Reset();

            return action;
        }

        public void ResetPrfiler()
        {
            stopwatch.Reset();

            calls = 0;
            time = 0;
        }
    }
}
