using System;
using System.Collections.Generic;
using System.Text;
using BangSimulator.Game;

namespace BangSimulator.Agent.Model
{
    public class AgentAction
    {
        public Card? PlayedCard { get; set; }
        public int target { get; set; }
    }

}
