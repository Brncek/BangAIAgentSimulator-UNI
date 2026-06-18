
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent
{
    public interface IAgent
    {

        public AgentAction Step(GameInfo gameInfo);

        public void GameOver(PlayerRole winingRole);

        public void Reset();

        public void SetEval(bool eval);

        public bool HasReward();

        public List<float> GetRewards();

        public void Save(string pathFolder);

        public void Load(string path);
    }
}
