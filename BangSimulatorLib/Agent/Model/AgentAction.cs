using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent.Model
{
    public class AgentAction
    {
        public Card? PlayedCard { get; set; }
        public int target { get; set; }


        public override string ToString()
        {
            return "PlayedCard: " + (PlayedCard != null ? PlayedCard.ToString() : "None") + ", Target: " + target;
        }
    }

}
