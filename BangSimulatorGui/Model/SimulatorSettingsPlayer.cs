using System;
using System.Collections.Generic;
using System.Text;

namespace BangSimulatorGui.Model
{
    public  class SimulatorSettingsPlayer
    {
        public AgentRole Role { get; set; }
        public AgentType Type { get; set; }
        public bool UseStep { get; set; }
    
        public int PythonId { get; set; }
        public string SavePath { get; set; } = string.Empty;
        public string LoadPath { get; set; } = string.Empty;
    
        public bool EvalMode { get; set; }
        public bool PyTerminal { get; set; }

        public string OllamaUrl { get; set; }  = string.Empty;
        public string Model { get; set; } = string.Empty;

    }
}
