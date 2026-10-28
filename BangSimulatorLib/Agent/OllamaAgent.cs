using System;
using System.Collections.Generic;
using System.Text;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent
{
    public class OllamaAgent : IAgent
    {
        private string url;
        private string model;

        public OllamaAgent(string url, string model)
        {
            this.url = url;
            this.model = model;

            //TODO: test ollama
        }

        public void GameOver(PlayerRole winingRole)
        {
            //TODO:
        }

        public AgentAction Step(GameInfo gameInfo)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            //TODO: new conversation
        }


        //------------------------------------------------------------------------------------------

        public List<float> GetRewards() { return []}
        public bool HasReward() { return false}
        public void Load(string path) { }
        public void SetAutoSavePath(string pathFolder) { }
        public void SetEval(bool eval) { }
        public void SetUseMemory(bool use) { }
    }
}
