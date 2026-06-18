using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;


namespace BangSimulatorLib.Agent
{
    public class PPOAgent : IAgent
    {
        //NOTE:: implement

        public void GameOver(PlayerRole winingRole)
        {
            throw new NotImplementedException();
        }

        public List<float> GetRewards()
        {
            throw new NotImplementedException();
        }

        public bool HasReward() => true;

        public void Load(string path)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public void Save(string pathFolder)
        {
            throw new NotImplementedException();
        }

        public void SetEval(bool eval)
        {
            throw new NotImplementedException();
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            throw new NotImplementedException();
        }
    }
}
