using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BangSimulatorLib.Game;

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
    var candidates = Directory.GetFiles(options.CheckpointDirectory, "*.pth")
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(path => new Checkpoint(path, GetRole(path)))
        .ToArray();

    if (candidates.Length == 0)
        throw new InvalidOperationException("The checkpoint folder contains no .pth files.");

    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    Environment.SetEnvironmentVariable("OMP_NUM_THREADS", Environment.GetEnvironmentVariable("OMP_NUM_THREADS") ?? "1");
    Environment.SetEnvironmentVariable("MKL_NUM_THREADS", Environment.GetEnvironmentVariable("MKL_NUM_THREADS") ?? "1");

    using var evaluator = new RankingEvaluator(options.Python, candidates);
    evaluator.ValidatePool(options.Pool);
    int plannedChecks = 1 + (options.Games - 1) / options.Batch;
    IRankingMethod method = options.Method switch
    {
        "fixed" => new FixedWinRateRanking(candidates),
        "uncertainty" => new UncertaintyRanking(candidates, plannedChecks, options.Precision),
        _ => throw new ArgumentException($"Unknown ranking method {options.Method}.")
    };

    var cancelInput = json ? Task.Run(Console.ReadLine) : null;
    if (!json)
        Console.WriteLine($"{options.Method} | {options.Pool} | seed {options.Seed} | up to {options.Games:N0} games per checkpoint");
    var seeds = new Random(options.Seed);
    var timer = Stopwatch.StartNew();
    int completed = 0;
    while (completed < options.Games)
    {
        int batchEnd = completed + Math.Min(options.Batch, options.Games - completed);
        while (completed < batchEnd)
        {
            int seed = seeds.Next();
            foreach (var candidate in candidates)
            {
                if (cancelInput is { IsCompletedSuccessfully: true, Result: "cancel" })
                    return 130;

                method.Observe(evaluator.Play(candidate, options.Pool, seed));
            }

            completed++;
        }

        var standings = method.GetStandings();
        var groups = GetGroups(standings);
        long matches = (long)completed * candidates.Length;
        long draws = standings.Sum(row => (long)row.Draws);
        bool targetReached = method.HasReachedTarget;
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                GamesPerCheckpoint = completed,
                Matches = matches,
                Draws = draws,
                ElapsedSeconds = timer.Elapsed.TotalSeconds,
                TargetReached = targetReached,
                Rows = groups.SelectMany(group => RankGroup(group).Select(item => new
                {
                    Role = group.Key.ToString(),
                    item.Rank,
                    item.Row.Candidate.Name,
                    item.Row.Wins,
                    item.Row.Games,
                    item.Row.Draws,
                    Score = item.Row.Score.ToString(item.Row.ScoreFormat),
                    Interval = FormatInterval(item.Row).Trim()
                }))
            }));
            Console.Out.Flush();
        }
        else
        {
            Console.WriteLine($"{completed:N0} games/checkpoint, {matches:N0} matches, {draws:N0} draws, {timer.Elapsed:g}");
            foreach (var group in groups)
                Console.WriteLine($"  {group.Key}: {string.Join(", ", group.Select(row => $"{row.Candidate.Name} {row.Score.ToString(row.ScoreFormat)}{FormatInterval(row)}"))}");
        }

        if (targetReached)
            break;
    }

    if (json)
        return 0;

    if (options.Method == "uncertainty")
    {
        string outcome = method.HasReachedTarget ? "reached" : "unmet at game limit";
        Console.WriteLine($"95% simultaneous interval target ±{options.Precision:P1}: {outcome}.");
    }

    foreach (var group in GetGroups(method.GetStandings()))
    {
        Console.WriteLine($"\n{group.Key}");
        foreach (var (rank, row) in RankGroup(group))
        {
            string draws = row.Draws > 0 ? $" ({row.Draws:N0} draws)" : "";
            Console.WriteLine($"{rank,3}  {row.Candidate.Name,-24} {row.Wins,6}/{row.Games,-6} {row.Score.ToString(row.ScoreFormat),8}{FormatInterval(row)}{draws}");
        }

        var ordered = group.ToArray();
        if (ordered.Length > 1 && ordered.Zip(ordered.Skip(1)).Any(pair =>
            pair.First.LowerBound <= pair.Second.UpperBound))
            Console.WriteLine("  Some ranks remain unresolved because their intervals overlap.");
    }

    return 0;
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

static IEnumerable<IGrouping<PlayerRole, Standing>> GetGroups(IEnumerable<Standing> standings) =>
    standings.OrderByDescending(row => row.Score).ThenBy(row => row.Candidate.Name, StringComparer.OrdinalIgnoreCase)
        .GroupBy(row => row.Candidate.Role).OrderBy(group => group.Key);

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
    Console.WriteLine("Usage: BangSimulatorRanking <checkpoint-folder> [--method fixed|uncertainty] [--pool mixed|self]");
    Console.WriteLine("       [--games N] [--batch N] [--precision PERCENTAGE-POINTS] [--seed N] [--python EXECUTABLE] [--json]");
    Console.WriteLine("Defaults: fixed, mixed, 10000 games per checkpoint, batch 500, precision 5, seed 42, python from PATH.");
    Console.WriteLine("Mixed opponents: two role-matched checkpoints, one Random agent, one Scripted agent.");
    Console.WriteLine("Self opponents: four role-matched checkpoints.");
    Console.WriteLine($"Games that exceed {MatchActionBudget.Limit:N0} actions count as draws and contribute zero wins.");
}

internal sealed record RankingOptions(
    string CheckpointDirectory, string Python, string Method, OpponentPool Pool,
    int Games, int Batch, double Precision, int Seed)
{
    public static RankingOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Specify a checkpoint folder.");

        string method = "fixed";
        OpponentPool pool = OpponentPool.Mixed;
        int games = 10000;
        int batch = 500;
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

        return new RankingOptions(Path.GetFullPath(args[0]), python, method, pool, games, batch, precision, seed);
    }

    private static int PositiveInt(string value, string option)
    {
        if (!int.TryParse(value, out int number) || number <= 0)
            throw new ArgumentException($"{option} must be a positive integer.");

        return number;
    }
}
