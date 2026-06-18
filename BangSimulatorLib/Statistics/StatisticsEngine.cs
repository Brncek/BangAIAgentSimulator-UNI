using System;
using System.Collections.Generic;
using System.Text;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Statistics
{
    public class StatisticsEngine
    {

        public static (float SherifWins, float BanditWins, float RenegadeWins) WinsEval(List<GameResoult> resoults)
        {
            int sheriffWins = 0;
            int banditWins = 0;
            int renegadeWins = 0;

            foreach (var r in resoults)
            {
                switch(r.WinningRole)
                {
                    case PlayerRole.Sheriff: sheriffWins++; break;
                    case PlayerRole.Bandit: banditWins++; break;
                    default: renegadeWins++; break;
                }
            }

            return (sheriffWins / (float)resoults.Count * 100,
                    banditWins / (float)resoults.Count * 100,
                    renegadeWins / (float)resoults.Count * 100);
        }

        public static float[] AverageTurns(List<GameResoult> resoults, int averageWindow) 
        {
            List<float> AVGs = [];

            for (int i = 0; i < resoults.Count; i++)
            {
                int sum = 0;
                int count = 0;

                for (int j = 0; j < averageWindow; j++)
                {
                    if (i - j < 0) break;

                    sum += resoults[i - j].Turns;
                    count++;
                }

                AVGs.Add(sum / (float)count);
            }


            return AVGs.ToArray();
        }

        public static List<float[]> PlayersLifesProgress(GameResoult resoults)
        {
            var list = new List<float[]>();

            if (resoults.LivesData.Count > 0)
            {
                for (int i = 0; i < resoults.LivesData[0].Length; i++)
                {
                    list.Add(new float[resoults.LivesData.Count]);
                }
            }

            for (int i = 0; i < resoults.LivesData.Count; i++)
            {
                var lifeData = resoults.LivesData[i];

                for (int j = 0; j < lifeData.Length; j++)
                {
                    list[j][i] = lifeData[j];
                }
            }

            return list;
        }

        public static List<float[]> WinRatesAVGs(List<GameResoult> resoults, int AVGlen)
        {
            var list = new List<float[]>();

            for (int i = 0; i < 3; i++)
            {
                list.Add(new float[resoults.Count]);
            }

            for (int i = 0; i < resoults.Count; i++)
            {
                int scherofAvg = 0;
                int banditAvg = 0;
                int renegadeAvg = 0;

                int count = 0;

                for (int j = 0; j < AVGlen && i - j >= 0; j++)
                {
                    var winner = resoults[i - j].WinningRole;

                    switch(winner)
                    {
                        case PlayerRole.Sheriff:
                            scherofAvg++; break;
                        case PlayerRole.Bandit:
                            banditAvg++; break;
                        default:
                            renegadeAvg++; break;
                    }

                    count++;
                } 

                list[0][i] = scherofAvg / (float)count * 100;
                list[1][i] = banditAvg / (float)count * 100;
                list[2][i] = renegadeAvg / (float)count * 100;
            }

            return list;
        }
    }
}
