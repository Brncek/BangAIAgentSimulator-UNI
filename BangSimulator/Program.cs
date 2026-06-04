using BangSimulator.Agent;
using BangSimulator.Game;

namespace BangSimulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            List<IAgent> agents = new List<IAgent>();
            for (int i = 0; i < 4; i++)
            {
                agents.Add(new ScriptedAgent());
            }

            agents.Add(new ScriptedAgent());

            GameEngine game = new GameEngine(agents.ToArray());

            int sheriffWins = 0;
            int sheriffHins = 0;
            int renegadeWins = 0;

            int startTime = System.Environment.TickCount;

            for (int i = 0; i < 100000; i++)
            {
                var res = game.Play();
            
                if (i % 1000 == 0)
                {
                    Console.WriteLine(i);
                }

                if (res.WinningRole == PlayerRole.Sheriff)
                {
                    sheriffWins++;
                }
                else if (res.WinningRole == PlayerRole.Renegade)
                {
                    renegadeWins++;
                }
                else
                {
                    sheriffHins++;
                }
            }

            int endTime = System.Environment.TickCount;

            int elapsedTime = endTime - startTime;


            Console.WriteLine("Elapsed time: " + elapsedTime / 1000.0 + " seconds");
            Console.WriteLine("Sheriff wins: " + sheriffWins / 100000.0 * 100 + "%");
            Console.WriteLine("Renegade wins: " + renegadeWins / 100000.0 * 100 + "%");
            Console.WriteLine("Bandits wins: " + sheriffHins / 100000.0 * 100 + "%");

        }
    }
}
