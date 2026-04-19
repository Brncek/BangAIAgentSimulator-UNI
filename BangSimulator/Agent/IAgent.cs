
using BangSimulator.Agent.Model;

namespace BangSimulator.Agent
{
    public interface IAgent
    {
        //TODO: define agent interface

        public AgentAction Step(GameInfo gameInfo);

        public void Reset();

    }
}
