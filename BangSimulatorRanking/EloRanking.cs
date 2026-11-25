using BangSimulatorLib.Statistics;

internal sealed class EloRanking(IReadOnlyList<Checkpoint> candidates) : FixedWinRateRanking(candidates)
{
    private readonly Dictionary<Checkpoint, double> ratings =
        candidates.ToDictionary(candidate => candidate, _ => FactionElo.InitialRating);

    public override void Observe(MatchResult match)
    {
        base.Observe(match);
        var players = match.Lineup.Select(player => new EloParticipant(player.Role,
            player.Checkpoint is { } checkpoint ? ratings[checkpoint] : FactionElo.InitialRating)).ToArray();
        double[] changes = FactionElo.CalculateChanges(players, match.WinningRole);
        for (int index = 0; index < match.Lineup.Count; index++)
        {
            if (match.Lineup[index].Checkpoint is { } checkpoint)
                ratings[checkpoint] += changes[index];
        }
    }

    public override IReadOnlyList<Standing> GetStandings() =>
        base.GetStandings().Select(standing => standing with
        {
            Score = ratings[standing.Candidate],
            ScoreFormat = "F0",
            Rating = ratings[standing.Candidate]
        }).ToArray();
}
