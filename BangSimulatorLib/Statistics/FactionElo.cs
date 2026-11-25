using BangSimulatorLib.Game;

namespace BangSimulatorLib.Statistics;

public readonly record struct EloParticipant(PlayerRole Role, double Rating);

public static class FactionElo
{
    public const double InitialRating = 1500;
    public const double KFactor = 32;

    public static PlayerRole TeamFor(PlayerRole role) =>
        role == PlayerRole.Deputy ? PlayerRole.Sheriff : role;

    public static double[] CalculateChanges(IReadOnlyList<EloParticipant> players, PlayerRole? winner)
    {
        var changes = new double[players.Count];
        if (winner is null || players.Count == 0)
            return changes;

        var teams = players.Select((player, index) => (Player: player, Index: index))
            .GroupBy(entry => TeamFor(entry.Player.Role))
            .Select(group => new
            {
                Role = group.Key,
                Members = group.ToArray(),
                Rating = group.Average(entry => entry.Player.Rating)
            }).ToArray();

        double highestRating = teams.Max(team => team.Rating);
        double[] strengths = teams.Select(team => Math.Pow(10, (team.Rating - highestRating) / 400)).ToArray();
        double totalStrength = strengths.Sum();
        for (int index = 0; index < teams.Length; index++)
        {
            var team = teams[index];
            double actual = winner == team.Role ? 1 : 0;
            double expected = strengths[index] / totalStrength;
            double changePerSeat = KFactor * (actual - expected) / team.Members.Length;
            foreach (var member in team.Members)
                changes[member.Index] = changePerSeat;
        }

        return changes;
    }
}
