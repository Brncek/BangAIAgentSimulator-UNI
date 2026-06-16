using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BangSimulatorLib.Game
{
    public class GlobalRnd
    {
        public static Random Rnd { get; private set; } 


        static GlobalRnd()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("GameConfig.json", optional: false, reloadOnChange: true)
                .Build();

            int seed = configuration.GetValue<int>("RndSeed");

            if (seed == -1)
                Rnd = new Random();
            else
                Rnd = new Random(seed);
        }

        public static void SetSeed(int seed)
        {
            if (seed == -1)
                Rnd = new Random();
            else
                Rnd = new Random(seed);
        }
    }
}
