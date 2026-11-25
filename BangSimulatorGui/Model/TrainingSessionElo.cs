using System.IO;
using BangSimulatorLib.Game;
using BangSimulatorLib.Statistics;

namespace BangSimulatorGui.Model;

internal sealed class TrainingSessionElo(Player[] players, SimulatorSettingsPlayer[] settings)
{
    private readonly double[] ratings = Enumerable.Repeat(FactionElo.InitialRating, players.Length).ToArray();
    private readonly int[] wins = new int[players.Length];
    private int games;
    private SimulatorSettingsPlayer[] playerSettings = settings;

    public void UpdateSettings(SimulatorSettingsPlayer[] settings)
    {
        if (settings.Length != players.Length)
            throw new ArgumentException("Session settings must match the player roster.");
        playerSettings = settings;
    }

    public void Observe(PlayerRole winner)
    {
        var participants = players.Select((player, index) => new EloParticipant(player.Role, ratings[index])).ToArray();
        double[] changes = FactionElo.CalculateChanges(participants, winner);
        games++;
        for (int index = 0; index < players.Length; index++)
        {
            if (IsRated(playerSettings[index].Type))
                ratings[index] += changes[index];
            if (FactionElo.TeamFor(players[index].Role) == winner)
                wins[index]++;
        }
    }

    public SessionEloRow[] Snapshot() =>
        players.Select((player, index) =>
        {
            var setting = playerSettings[index];
            bool rated = IsRated(setting.Type);
            string path = setting.EvalMode ? setting.LoadPath : setting.SavePath;
            string modelPath = ModelPath(path);
            string mode = rated ? (setting.EvalMode ? "Evaluation" : "Training") : "Baseline";
            return new SessionEloRow($"Player {index + 1} ({setting.Type})", player.Role.ToString(),
                modelPath, ratings[index], games, wins[index], mode, rated);
        }).OrderByDescending(row => row.Elo).ToArray();

    private static bool IsRated(AgentType type) =>
        type is not AgentType.Random and not AgentType.Scripted and not AgentType.TurboScripted;

    private static string ModelPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return "";
        }
        catch (NotSupportedException)
        {
            return "";
        }
    }
}

internal sealed record SessionEloRow(
    string Player, string Role, string ModelPath, double Elo, int Games, int Wins, string Mode, bool Rated)
{
    public string Model => string.IsNullOrEmpty(ModelPath) ? "—" : Path.GetFileName(ModelPath);
    public string WinRate => Games == 0 ? "—" : ((double)Wins / Games).ToString("P1");
}
