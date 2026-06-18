using System;
using System.Collections.Generic;
using System.Text;

namespace BangSimulatorGui.Model
{
    public enum AgentType
    {
        Random,
        Scripted,
        Python,
        NetDQN
    }

    public enum AgentRole
    {
        NONE = -1,
        Sheriff = 0,
        Deputy = 1,
        Bandit = 2,
        Renegade = 3,
    }
}
