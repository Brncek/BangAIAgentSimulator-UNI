
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent
{
    public interface IAgent
    {

        public AgentAction Step(GameInfo gameInfo);

        public void GameOver(PlayerRole winingRole);

        public void Reset();

        public bool HasReward();

        public double GetCumulativeReward();

    }
}
