internal interface IRankingMethod
{
    void Observe(MatchResult match);
    IReadOnlyList<Standing> GetStandings();
    bool HasReachedTarget { get; }
}

internal sealed record Standing(Checkpoint Candidate, int Wins, int Games, int Draws, double Score,
    double? LowerBound = null, double? UpperBound = null, string ScoreFormat = "P1");

internal class FixedWinRateRanking(IReadOnlyList<Checkpoint> candidates) : IRankingMethod
{
    private readonly Dictionary<Checkpoint, (int Wins, int Games, int Draws)> counts =
        candidates.ToDictionary(candidate => candidate, _ => (0, 0, 0));

    public void Observe(MatchResult match)
    {
        var count = counts[match.Candidate];
        counts[match.Candidate] = (
            count.Wins + (match.CandidateWon ? 1 : 0),
            count.Games + 1,
            count.Draws + (match.WinningRole is null ? 1 : 0));
    }

    public virtual IReadOnlyList<Standing> GetStandings() =>
        counts.Select(entry => new Standing(entry.Key, entry.Value.Wins, entry.Value.Games, entry.Value.Draws,
            (double)entry.Value.Wins / entry.Value.Games)).ToArray();

    public virtual bool HasReachedTarget => false;
}

internal sealed class UncertaintyRanking(
    IReadOnlyList<Checkpoint> candidates, int plannedChecks, double targetRadius) : FixedWinRateRanking(candidates)
{
    // Split the error budget across candidates, both tails, and every scheduled look.
    private readonly double logTerm = Math.Log(2.0 * candidates.Count * plannedChecks / 0.05);

    public override bool HasReachedTarget => GetStandings().All(standing =>
        standing.Score - standing.LowerBound <= targetRadius &&
        standing.UpperBound - standing.Score <= targetRadius);

    public override IReadOnlyList<Standing> GetStandings() =>
        base.GetStandings().Select(standing =>
        {
            double limit = logTerm / standing.Games;
            return standing with
            {
                LowerBound = 1 - FindUpperBound(1 - standing.Score, limit),
                UpperBound = FindUpperBound(standing.Score, limit)
            };
        }).ToArray();

    // Invert the binomial Chernoff bound. The lower bound is the complement
    // of the upper bound on the failure rate.
    private static double FindUpperBound(double rate, double limit)
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
