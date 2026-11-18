using BangSimulatorLib.Agent;
using BangSimulatorLib.Game;

internal sealed class RankingEvaluator(string python, IReadOnlyList<Checkpoint> candidates) : IDisposable
{
    private static readonly PlayerRole[] Roles =
        [PlayerRole.Sheriff, PlayerRole.Deputy, PlayerRole.Bandit, PlayerRole.Bandit, PlayerRole.Renegade];

    private readonly Dictionary<PlayerRole, Checkpoint[]> checkpointsByRole =
        candidates.GroupBy(candidate => candidate.Role).ToDictionary(group => group.Key, group => group.ToArray());

    private readonly Dictionary<(Checkpoint Checkpoint, int Instance), PythonAgent> agents = [];

    public void ValidatePool(OpponentPool pool)
    {
        var missingRoles = Roles.Distinct().Where(role => !checkpointsByRole.ContainsKey(role)).ToArray();
        if (missingRoles.Length > 0)
            throw new InvalidOperationException($"{pool} play requires a checkpoint for each role. Missing {string.Join(", ", missingRoles)}.");
    }

    public MatchResult Play(OpponentPool pool, int trialSeed)
    {
        var (lineup, gameSeed) = Schedule(pool, trialSeed);
        var usedInstances = new Dictionary<Checkpoint, int>();
        var budget = new MatchActionBudget();
        var players = lineup.Select(participant =>
            new Player(participant.Role, new BudgetedAgent(GetAgent(participant, usedInstances), budget))).ToArray();

        GlobalRnd.SetSeed(gameSeed);
        PlayerRole? winner = null;
        try
        {
            winner = new GameEngine(players).Play().WinningRole;
        }
        catch (MatchActionLimitException)
        {
            // The engine has no turn limit. Count the capped match as a draw.
        }

        return new MatchResult(lineup, winner, trialSeed);
    }

    internal (Participant[] Lineup, int GameSeed) Schedule(OpponentPool pool, int trialSeed)
    {
        var schedule = new Random(trialSeed);
        var participants = new Participant[Roles.Length];
        // Only one in ten Mixed games gets a baseline, and only in one seat.
        int baselineSeat = pool == OpponentPool.Mixed && schedule.Next(10) == 0 ? schedule.Next(Roles.Length) : -1;
        var baselineKind = schedule.Next(2) == 0 ? OpponentKind.Random : OpponentKind.Scripted;
        for (int seat = 0; seat < Roles.Length; seat++)
        {
            PlayerRole role = Roles[seat];
            if (seat == baselineSeat)
            {
                participants[seat] = new Participant(role, baselineKind);
                continue;
            }

            Checkpoint[] roleChoices = checkpointsByRole[role];
            // Prefer different versions in the two Bandit seats. A single
            // available Bandit can occupy both seats and is scored once.
            Checkpoint[] choices = roleChoices.Length > 1
                ? roleChoices.Where(choice => !participants.Take(seat).Any(player => player?.Checkpoint == choice)).ToArray()
                : roleChoices;
            participants[seat] = new Participant(role, OpponentKind.Checkpoint, choices[schedule.Next(choices.Length)]);
        }

        var order = Enumerable.Range(0, Roles.Length).ToArray();
        schedule.Shuffle(order);
        return (order.Select(seat => participants[seat]).ToArray(), schedule.Next());
    }

    private IAgent GetAgent(Participant participant, Dictionary<Checkpoint, int> usedInstances)
    {
        if (participant.Kind == OpponentKind.Random)
            return new RandomAgent();

        if (participant.Kind == OpponentKind.Scripted)
            return new ScriptedAgent();

        var checkpoint = participant.Checkpoint!;
        int instance = usedInstances.GetValueOrDefault(checkpoint);
        usedInstances[checkpoint] = instance + 1;
        var key = (checkpoint, instance);
        if (agents.TryGetValue(key, out var agent))
            return agent;

        Console.Error.WriteLine($"Loading {checkpoint.Name} (instance {instance + 1})");
        agent = new PythonAgent(Console.Error.WriteLine, false, pythonExecutable: python);
        try
        {
            agent.LoadForEvaluation(checkpoint.Path, Roles.Length);
            agents.Add(key, agent);
            return agent;
        }
        catch
        {
            agent.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var agent in agents.Values)
            agent.Dispose();
    }
}

internal enum OpponentPool { Mixed, Self }
internal enum OpponentKind { Random, Scripted, Checkpoint }

internal sealed record Checkpoint(string Path, PlayerRole Role)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

internal sealed record Participant(PlayerRole Role, OpponentKind Kind, Checkpoint? Checkpoint = null);

internal sealed record MatchResult(
    IReadOnlyList<Participant> Lineup, PlayerRole? WinningRole, int Seed)
{
    public static PlayerRole TeamFor(PlayerRole role) =>
        role == PlayerRole.Deputy ? PlayerRole.Sheriff : role;
}
