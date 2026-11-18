internal sealed class EloRanking(IReadOnlyList<Checkpoint> candidates) : FixedWinRateRanking(candidates)
{
    private const double InitialRating = 1500;
    private const double KFactor = 32;
    private readonly Dictionary<Checkpoint, double> ratings =
        candidates.ToDictionary(candidate => candidate, _ => InitialRating);

    public override void Observe(MatchResult match)
    {
        base.Observe(match);
        if (match.WinningRole is null)
            return;

        var teams = match.Lineup.GroupBy(player => MatchResult.TeamFor(player.Role))
            .Select(group => new
            {
                Role = group.Key,
                Players = group.ToArray(),
                Rating = group.Average(player => player.Checkpoint is { } checkpoint
                    ? ratings[checkpoint] : InitialRating)
            }).ToArray();

        // This extends the Elo logistic scale to three factions. Calculate
        // expectations from the pre-match team means, with a stable softmax.
        double highestRating = teams.Max(team => team.Rating);
        double[] strengths = teams.Select(team => Math.Pow(10, (team.Rating - highestRating) / 400)).ToArray();
        double totalStrength = strengths.Sum();
        for (int index = 0; index < teams.Length; index++)
        {
            var team = teams[index];
            double actual = match.WinningRole == team.Role ? 1 : 0;
            double expected = strengths[index] / totalStrength;
            double changePerSeat = KFactor * (actual - expected) / team.Players.Length;
            foreach (var player in team.Players)
            {
                if (player.Checkpoint is { } checkpoint)
                    ratings[checkpoint] += changePerSeat;
            }
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
