namespace BangSimulatorLib.Game
{
    public class GameResoult
    {
        public PlayerRole WinningRole { get; set; }
        
        public Player[] WinningPlayers { get; set; } = [];
        
        public int Turns { get; set; } = 0;
        
        public List<int[]> LivesData { get; set; } = [];

        public int[,] PlayerToPlayerBang { get; set; } = new int[0,0];

        public DeckMemory[] LastRoundmemory { get; set; } = [];

        public override string ToString()
        {
            return $"Winning Role: {WinningRole}, Winning Players: {string.Join(", ", WinningPlayers.Select(p => p.Id))}";
        }
    }

    public class GameResoultSerializable
    {

        public GameResoultSerializable(GameResoult g)
        {
            WinningRole = g.WinningRole;
            WinningPlayers = g.WinningPlayers;
            Turns = g.Turns;
            LivesData = g.LivesData;
            LastRoundmemory = g.LastRoundmemory;

            for (int i = 0; i < g.PlayerToPlayerBang.GetLength(0); i++)
            {
                PlayerToPlayerBang.Add(new int[g.PlayerToPlayerBang.GetLength(1)]);

                for (int j = 0; j < g.PlayerToPlayerBang.GetLength(1); j++)
                {
                    PlayerToPlayerBang[i][j] = g.PlayerToPlayerBang[i,j];
                }
            }

        }

        public PlayerRole WinningRole { get; set; }

        public Player[] WinningPlayers { get; set; } = [];

        public int Turns { get; set; } = 0;

        public List<int[]> LivesData { get; set; } = [];

        List<int[]> PlayerToPlayerBang { get; set; } = [];

        public DeckMemory[] LastRoundmemory { get; set; } = [];

    }
}
