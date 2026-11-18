using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BangSimulatorLib.Game;

Console.OutputEncoding = new UTF8Encoding(false);
bool json = args.Contains("--json", StringComparer.Ordinal);
args = args.Where(arg => arg != "--json").ToArray();
if (args.Length == 1 && args[0] is "--help" or "-h")
{
    PrintUsage();
    return 0;
}

try
{
    var options = RankingOptions.Parse(args);
    var catalog = Directory.GetFiles(options.CheckpointDirectory, "*.pth")
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(path => new Checkpoint(path, GetRole(path)))
        .ToArray();

    if (catalog.Length == 0)
        throw new InvalidOperationException("The checkpoint folder contains no .pth files.");

    var target = options.Compare is null ? null : catalog.SingleOrDefault(candidate =>
        candidate.Name.Equals(options.Compare, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Checkpoint {options.Compare} was not found in the selected folder.");
    var selectedRole = target?.Role ?? options.Role;
    var candidates = selectedRole is null ? catalog : catalog.Where(candidate => candidate.Role == selectedRole).ToArray();
    if (candidates.Length == 0)
        throw new ArgumentException($"No checkpoints found for {selectedRole}.");
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    Environment.SetEnvironmentVariable("OMP_NUM_THREADS", Environment.GetEnvironmentVariable("OMP_NUM_THREADS") ?? "1");
    Environment.SetEnvironmentVariable("MKL_NUM_THREADS", Environment.GetEnvironmentVariable("MKL_NUM_THREADS") ?? "1");

    using var evaluator = new RankingEvaluator(options.Python, catalog);
    evaluator.ValidatePool(options.Pool);
    int plannedChecks = 1 + (options.Games - 1) / options.Batch;
    IRankingMethod method = options.Method switch
    {
        "fixed" => new FixedWinRateRanking(catalog),
        "elo" => new EloRanking(catalog),
        "elo-overall" => new EloRanking(catalog),
        "uncertainty" => new UncertaintyRanking(catalog, plannedChecks, options.Precision, rankedCandidates: candidates),
        "comparison" => new ComparisonRanking(catalog, candidates, plannedChecks, options.Precision, target?.Name),
        _ => throw new ArgumentException($"Unknown ranking method {options.Method}.")
    };

    using var cancellation = new CancellationTokenSource();
    if (json)
    {
        _ = Task.Run(() =>
        {
            if (Console.ReadLine() == "cancel")
            {
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        });
    }

    if (!json)
        Console.WriteLine($"{options.Method} | {options.Pool} | seed {options.Seed} | up to {options.Games:N0} total matches");
    var seeds = new Random(options.Seed);
    var timer = Stopwatch.StartNew();
    int completed = 0;
    long drawnMatches = 0;
    if (json)
        Console.Error.WriteLine($"Preparing {catalog.Length} saved agents. Every participating saved agent receives its match result.");
    while (completed < options.Games)
    {
        int batchEnd = completed + Math.Min(options.Batch, options.Games - completed);
        while (completed < batchEnd)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var match = evaluator.Play(options.Pool, seeds.Next());
            method.Observe(match);
            if (match.WinningRole is null)
                drawnMatches++;
            completed++;
            if (json)
                Console.Error.WriteLine(
                    $"{completed:N0}/{options.Games:N0} matches completed | " +
                    $"elapsed {timer.Elapsed:hh\\:mm\\:ss}. Next leaderboard at {batchEnd:N0} matches.");
        }

        var standings = method.GetStandings();
        var groups = GetGroups(standings, options.Method == "elo-overall");
        long matches = completed;
        long draws = drawnMatches;
        bool targetReached = method.HasReachedTarget;
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Matches = matches,
                Draws = draws,
                ElapsedSeconds = timer.Elapsed.TotalSeconds,
                TargetReached = targetReached,
                Comparisons = method is ComparisonRanking comparison ? comparison.GetComparisons() : [],
                Rows = groups.SelectMany(group => RankGroup(group).Select(item => new
                {
                    Role = item.Row.Candidate.Role.ToString(),
                    item.Rank,
                    item.Row.Candidate.Name,
                    item.Row.Wins,
                    item.Row.Games,
                    item.Row.Draws,
                    Score = item.Row.Games == 0 ? "—" : item.Row.Score.ToString(item.Row.ScoreFormat),
                    WinRate = item.Row.Games == 0 ? "—" : ((double)item.Row.Wins / item.Row.Games).ToString("P1"),
                    Elo = item.Row.Rating?.ToString("F0") ?? "",
                    Interval = FormatInterval(item.Row).Trim()
                }))
            }));
            Console.Out.Flush();
        }
        else
        {
            Console.WriteLine($"{matches:N0} matches, {draws:N0} draws, {timer.Elapsed:g}");
            foreach (var group in groups)
                Console.WriteLine($"  {group.Key}: {string.Join(", ", group.Select(row => $"{row.Candidate.Name} {row.Score.ToString(row.ScoreFormat)}{FormatInterval(row)}"))}");
        }

        if (targetReached)
            break;
    }

    if (json)
        return 0;

    if (method is ComparisonRanking comparisonResults)
    {
        foreach (string result in comparisonResults.GetComparisons())
            Console.WriteLine(result);
        Console.WriteLine(method.HasReachedTarget ? "All comparisons resolved." : "Game limit reached. Some comparisons remain uncertain.");
    }
    if (options.Method == "uncertainty")
    {
        string outcome = method.HasReachedTarget ? "reached" : "unmet at game limit";
        Console.WriteLine($"95% simultaneous interval target ±{options.Precision:P1}: {outcome}.");
    }

    foreach (var group in GetGroups(method.GetStandings(), options.Method == "elo-overall"))
    {
        Console.WriteLine($"\n{group.Key}");
        foreach (var (rank, row) in RankGroup(group))
        {
            string draws = row.Draws > 0 ? $" ({row.Draws:N0} draws)" : "";
            string role = options.Method == "elo-overall" ? $"{row.Candidate.Role,-9} " : "";
            Console.WriteLine($"{rank,3}  {role}{row.Candidate.Name,-24} {row.Wins,6}/{row.Games,-6} {row.Score.ToString(row.ScoreFormat),8}{FormatInterval(row)}{draws}");
        }

        var ordered = group.ToArray();
        if (method is not ComparisonRanking && ordered.Length > 1 && ordered.Zip(ordered.Skip(1)).Any(pair =>
            pair.First.LowerBound <= pair.Second.UpperBound))
            Console.WriteLine("  Some ranks remain unresolved because their intervals overlap.");
    }

    return 0;
}
catch (OperationCanceledException)
{
    return 130;
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    if (!json)
        PrintUsage();
    return 2;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Ranking failed. {error.Message}");
    return 1;
}

static IEnumerable<IGrouping<string, Standing>> GetGroups(IEnumerable<Standing> standings, bool overall = false) =>
    standings.OrderByDescending(row => row.Score).ThenBy(row => row.Candidate.Name, StringComparer.OrdinalIgnoreCase)
        .GroupBy(row => overall ? "Overall" : row.Candidate.Role.ToString())
        .OrderBy(group => overall ? 0 : (int)group.First().Candidate.Role);

static IEnumerable<(int Rank, Standing Row)> RankGroup(IEnumerable<Standing> group)
{
    int position = 0;
    int rank = 0;
    double? previousScore = null;
    foreach (var row in group)
    {
        position++;
        if (row.Score != previousScore)
            rank = position;

        yield return (rank, row);
        previousScore = row.Score;
    }
}

static string FormatInterval(Standing row) =>
    row.LowerBound is double lower && row.UpperBound is double upper
        ? $" [{lower.ToString(row.ScoreFormat)}, {upper.ToString(row.ScoreFormat)}]"
        : "";

static PlayerRole GetRole(string path)
{
    string name = Path.GetFileNameWithoutExtension(path);
    foreach (var role in Enum.GetValues<PlayerRole>())
    {
        if (name.StartsWith($"{role}_", StringComparison.OrdinalIgnoreCase))
            return role;
    }

    throw new InvalidOperationException($"Name {Path.GetFileName(path)} with a sheriff_, deputy_, bandit_, or renegade_ prefix.");
}

static void PrintUsage()
{
    Console.WriteLine("Usage: BangSimulatorRanking <checkpoint-folder> [--method fixed|uncertainty|comparison|elo|elo-overall] [--pool mixed|self]");
    Console.WriteLine("       [--role Sheriff|Deputy|Bandit|Renegade] [--compare CHECKPOINT.pth] [--games N] [--batch N] [--precision PERCENTAGE-POINTS] [--seed N] [--python EXECUTABLE] [--json]");
    Console.WriteLine("Defaults: fixed, mixed, all roles, 2000 total matches, batch 50, precision 5, seed 42.");
    Console.WriteLine("Every participating saved agent is scored. Comparison uses win-rate differences; precision is its equivalence margin.");
    Console.WriteLine("Elo: faction-team ratings start at 1500 each run, K=32, team strength uses mean rating.");
    Console.WriteLine("elo ranks within roles. elo-overall places every saved agent in one leaderboard using the same ratings.");
    Console.WriteLine("Random and Scripted baselines stay at 1500. Capped draws leave ratings unchanged.");
    Console.WriteLine("Five players per game: one Sheriff, one Deputy, two Bandits, one Renegade.");
    Console.WriteLine("Mixed: 90% all saved agents, 10% one Random or Scripted agent plus four saved agents.");
    Console.WriteLine("Self-play: five saved agents.");
    Console.WriteLine($"Games that exceed {MatchActionBudget.Limit:N0} actions count as draws and contribute zero wins.");
}

internal sealed record RankingOptions(
    string CheckpointDirectory, string Python, string Method, OpponentPool Pool,
    int Games, int Batch, double Precision, int Seed, string? Compare, PlayerRole? Role)
{
    public static RankingOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Specify a checkpoint folder.");

        string method = "fixed";
        OpponentPool pool = OpponentPool.Mixed;
        int games = 2000;
        int batch = 50;
        string? compare = null;
        PlayerRole? role = null;
        double precision = 0.05;
        int seed = 42;
        string python = "python";

        int index = 1;
        // Accept the previous positional game count and Python path.
        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
            games = PositiveInt(args[index++], "Games");
        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
            python = args[index++];

        for (; index < args.Length; index += 2)
        {
            string option = args[index];
            if (index + 1 == args.Length)
                throw new ArgumentException($"Missing value for {option}.");

            string value = args[index + 1];
            switch (option)
            {
                case "--method":
                    method = value;
                    break;
                case "--compare":
                    compare = value;
                    break;
                case "--role":
                    if (!Enum.TryParse<PlayerRole>(value, true, out var parsedRole) ||
                        !Enum.IsDefined(parsedRole))
                        throw new ArgumentException($"Unknown role {value}.");
                    role = parsedRole;
                    break;
                case "--pool":
                    pool = value switch
                    {
                        "mixed" => OpponentPool.Mixed,
                        "self" => OpponentPool.Self,
                        _ => throw new ArgumentException($"Unknown opponent pool {value}.")
                    };
                    break;
                case "--games":
                    games = PositiveInt(value, "Games");
                    break;
                case "--batch":
                    batch = PositiveInt(value, "Batch");
                    break;
                case "--precision":
                    if (!double.TryParse(value, CultureInfo.InvariantCulture, out double points) || !(points > 0 && points < 100))
                        throw new ArgumentException("Precision must be greater than 0 and less than 100 percentage points.");
                    precision = points / 100;
                    break;
                case "--seed":
                    if (!int.TryParse(value, out seed) || seed < 0)
                        throw new ArgumentException("Seed must be a nonnegative integer.");
                    break;
                case "--python":
                    python = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option {option}.");
            }
        }

        if (Path.IsPathRooted(python) || python.Contains(Path.DirectorySeparatorChar) || python.Contains(Path.AltDirectorySeparatorChar))
        {
            python = Path.GetFullPath(python);
            if (!File.Exists(python))
                throw new ArgumentException($"Python executable does not exist: {python}");
        }

        if (compare is not null && role is not null)
            throw new ArgumentException("Choose either --role or --compare.");

        return new RankingOptions(Path.GetFullPath(args[0]), python, method, pool, games, batch, precision, seed, compare, role);
    }

    private static int PositiveInt(string value, string option)
    {
        if (!int.TryParse(value, out int number) || number <= 0)
            throw new ArgumentException($"{option} must be a positive integer.");

        return number;
    }
}
