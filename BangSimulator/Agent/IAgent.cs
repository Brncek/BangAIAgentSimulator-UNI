
using BangSimulator.Agent.Model;
using BangSimulator.Game;

namespace BangSimulator.Agent
{
    public interface IAgent
    {

        public AgentAction Step(GameInfo gameInfo);

        public void GameOver(PlayerRole winingRole);

        public void Reset();

    }
}
