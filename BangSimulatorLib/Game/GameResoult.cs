namespace BangSimulatorLib.Game
{
    public class GameResoult
    {
        public PlayerRole WinningRole { get; set; }
        
        public Player[] WinningPlayers { get; set; } = [];
        
        public int Turns { get; set; } = 0;
        
        public List<int[]> LivesData { get; set; } = [];

        public int[,] PlayerToPlayerBang { get; set; } = new int[0,0];

        public override string ToString()
        {
            return $"Winning Role: {WinningRole}, Winning Players: {string.Join(", ", WinningPlayers.Select(p => p.Id))}";
        }
    }
}
