using System;
using System.Collections.Generic;
using System.Text;

namespace BangSimulatorGui.Model
{
    public class SimulatorSettings
    {
        public List<SimulatorSettingsPlayer> Players { get; set; } = [];
        public int Rounds { get; set; }
        public int MemSize { get; set; }
        public int MemLimit { get; set; }
        public int RndSeed { get; set; } 
        public int AvgLen { get; set; }

    }
}
