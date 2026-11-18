internal sealed class ComparisonRanking : UncertaintyRanking
{
    private readonly List<(Checkpoint First, Checkpoint Second)> pairs = [];
    private readonly double margin;

    public ComparisonRanking(
        IReadOnlyList<Checkpoint> catalog, IReadOnlyList<Checkpoint> rankedCandidates,
        int plannedChecks, double margin, string? target)
        : base(catalog, plannedChecks, margin)
    {
        this.margin = margin;
        foreach (var group in rankedCandidates.GroupBy(candidate => candidate.Role))
        {
            var roleCandidates = group.ToArray();
            for (int first = 0; first < roleCandidates.Length; first++)
            {
                for (int second = first + 1; second < roleCandidates.Length; second++)
                {
                    var a = roleCandidates[first];
                    var b = roleCandidates[second];
                    if (target is null || a.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                        b.Name.Equals(target, StringComparison.OrdinalIgnoreCase))
                        pairs.Add((a, b));
                }
            }
        }

        if (pairs.Count == 0)
            throw new ArgumentException("Comparison needs at least two saved agents with the same role.");
    }

    public override bool HasReachedTarget
    {
        get
        {
            var standings = GetStandings().ToDictionary(row => row.Candidate);
            return pairs.All(pair =>
            {
                var first = standings[pair.First];
                var second = standings[pair.Second];
                if (first.Games == 0 || second.Games == 0)
                    return false;

                var (lower, upper) = DifferenceInterval(first, second);
                return lower > 0 || upper < 0 || (lower >= -margin && upper <= margin);
            });
        }
    }

    public string[] GetComparisons()
    {
        var standings = GetStandings().ToDictionary(row => row.Candidate);
        return pairs.Select(pair =>
        {
            var first = standings[pair.First];
            var second = standings[pair.Second];
            if (first.Games == 0 || second.Games == 0)
                return $"{pair.First.Name} vs {pair.Second.Name}: waiting for match results.";

            var (lower, upper) = DifferenceInterval(first, second);
            string outcome = lower > 0 ? $"{pair.First.Name} better" :
                upper < 0 ? $"{pair.Second.Name} better" :
                lower >= -margin && upper <= margin ? $"within {margin * 100:G3} percentage points" :
                "uncertain";
            double difference = first.Score - second.Score;
            return $"{pair.First.Name} vs {pair.Second.Name}: {outcome}. " +
                $"Difference {difference * 100:F1} pp, 95% interval " +
                $"[{lower * 100:F1} to {upper * 100:F1}] pp.";
        }).ToArray();
    }

    // Simultaneous win-rate bounds remain valid when participants share a
    // match or a team. Subtracting them needs no independence between agents.
    private static (double Lower, double Upper) DifferenceInterval(Standing first, Standing second) =>
        (first.LowerBound!.Value - second.UpperBound!.Value,
         first.UpperBound!.Value - second.LowerBound!.Value);
}
