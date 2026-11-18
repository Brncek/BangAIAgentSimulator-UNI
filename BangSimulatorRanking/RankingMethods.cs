internal interface IRankingMethod
{
    void Observe(MatchResult match);
    IReadOnlyList<Standing> GetStandings();
    bool HasReachedTarget { get; }
}

internal sealed record Standing(Checkpoint Candidate, int Wins, int Games, int Draws, double Score,
    double? LowerBound = null, double? UpperBound = null, string ScoreFormat = "P1", double? Rating = null);

internal class FixedWinRateRanking(IReadOnlyList<Checkpoint> candidates) : IRankingMethod
{
    private readonly Dictionary<Checkpoint, (int Wins, int Games, int Draws)> counts =
        candidates.ToDictionary(candidate => candidate, _ => (0, 0, 0));

    public virtual void Observe(MatchResult match)
    {
        foreach (var checkpoint in match.Lineup
            .Where(player => player.Kind == OpponentKind.Checkpoint && player.Checkpoint is not null)
            .Select(player => player.Checkpoint!)
            .Distinct())
        {
            var count = counts[checkpoint];
            counts[checkpoint] = (
                count.Wins + (match.WinningRole == MatchResult.TeamFor(checkpoint.Role) ? 1 : 0),
                count.Games + 1,
                count.Draws + (match.WinningRole is null ? 1 : 0));
        }
    }

    public virtual IReadOnlyList<Standing> GetStandings() =>
        counts.Select(entry => new Standing(entry.Key, entry.Value.Wins, entry.Value.Games, entry.Value.Draws,
            entry.Value.Games == 0 ? 0 : (double)entry.Value.Wins / entry.Value.Games)).ToArray();

    public virtual bool HasReachedTarget => false;
}

internal class UncertaintyRanking(
    IReadOnlyList<Checkpoint> candidates, int plannedChecks, double targetRadius,
    double errorBudget = 0.05, IReadOnlyList<Checkpoint>? rankedCandidates = null) : FixedWinRateRanking(candidates)
{
    // Split the error budget across candidates, both tails, and every scheduled look.
    private readonly double logTerm = Math.Log(2.0 * candidates.Count * plannedChecks / errorBudget);
    private readonly HashSet<Checkpoint> ranked = (rankedCandidates ?? candidates).ToHashSet();

    public override bool HasReachedTarget => GetStandings().Where(standing => ranked.Contains(standing.Candidate)).All(standing =>
        standing.Games > 0 &&
        standing.Score - standing.LowerBound <= targetRadius &&
        standing.UpperBound - standing.Score <= targetRadius);

    public override IReadOnlyList<Standing> GetStandings() =>
        base.GetStandings().Select(standing =>
        {
            if (standing.Games == 0)
                return standing with { LowerBound = 0, UpperBound = 1 };

            double limit = logTerm / standing.Games;
            return standing with
            {
                LowerBound = 1 - FindUpperBound(1 - standing.Score, limit),
                UpperBound = FindUpperBound(standing.Score, limit)
            };
        }).ToArray();

    // Invert the binomial Chernoff bound. The lower bound is the complement
    // of the upper bound on the failure rate.
    internal static double FindUpperBound(double rate, double limit)
    {
        if (rate == 1)
            return 1;

        double low = rate;
        double high = 1;
        for (int iteration = 0; iteration < 40; iteration++)
        {
            double midpoint = (low + high) / 2;
            double divergence = (1 - rate) * Math.Log((1 - rate) / (1 - midpoint));
            if (rate > 0)
                divergence += rate * Math.Log(rate / midpoint);

            if (divergence > limit)
                high = midpoint;
            else
                low = midpoint;
        }

        return high;
    }
}
