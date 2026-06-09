using BangSimulator.Agent;
using BangSimulator.Game;
using static MessagePack.GeneratedMessagePackResolver.BangSimulator;

namespace BangSimulator
{
    internal class Program
    {
        private static readonly int NumGames = 25000;

        static void Main(string[] args)
        {
            /*
            List<IAgent> agents = new List<IAgent>();
            for (int i = 0; i < 4; i++)
            {
                agents.Add(new RandomAgent());
            }

            agents.Add(new DQNAgentNet());

            GameEngine game = new GameEngine(agents.ToArray());

            */

            Player[] playerList = 
            {
                new Player(PlayerRole.Sheriff, new RandomAgent()),
                new Player(PlayerRole.Renegade, new RandomAgent()),
                new Player(PlayerRole.Bandit, new DQNAgentNet()),
                new Player(PlayerRole.Bandit, new RandomAgent()),
                new Player(PlayerRole.Deputy, new RandomAgent())
            };

            GameEngine game = new GameEngine(playerList);

            int sheriffWins = 0;
            int sheriffHins = 0;
            int renegadeWins = 0;

            int startTime = System.Environment.TickCount;

            for (int i = 0; i < NumGames; i++)
            {
                var res = game.Play();
            
                if (i % (NumGames / 10) == 0)
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
            Console.WriteLine("Sheriff wins: " + sheriffWins / (float)NumGames * 100 + "%");
            Console.WriteLine("Renegade wins: " + renegadeWins / (float)NumGames * 100 + "%");
            Console.WriteLine("Bandits wins: " + sheriffHins / (float)NumGames * 100 + "%");

        }
    }
}
