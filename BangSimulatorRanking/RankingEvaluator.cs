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
        if (pool == OpponentPool.Self && missingRoles.Length > 0)
            throw new InvalidOperationException($"Self-play requires a checkpoint for each role. Missing {string.Join(", ", missingRoles)}.");

        if (pool == OpponentPool.Mixed && Roles.Count(checkpointsByRole.ContainsKey) < 3)
            throw new InvalidOperationException("Mixed play requires checkpoints for at least two of each candidate's four opponent seats.");
    }

    public MatchResult Play(Checkpoint candidate, OpponentPool pool, int trialSeed)
    {
        var schedule = new Random(trialSeed);
        int focalSeat = Array.IndexOf(Roles, candidate.Role);
        int[] opponentSeats = Enumerable.Range(0, Roles.Length).Where(seat => seat != focalSeat).ToArray();
        schedule.Shuffle(opponentSeats);

        var participants = new Participant?[Roles.Length];
        participants[focalSeat] = new Participant(candidate.Role, OpponentKind.Checkpoint, candidate);
        int trainedSeats = 0;
        foreach (int seat in opponentSeats)
        {
            PlayerRole role = Roles[seat];
            if (pool == OpponentPool.Self ||
                (trainedSeats < 2 && checkpointsByRole.ContainsKey(role)))
            {
                Checkpoint[] choices = checkpointsByRole[role];
                participants[seat] = new Participant(role, OpponentKind.Checkpoint, choices[schedule.Next(choices.Length)]);
                trainedSeats++;
            }
        }

        bool randomPlaced = false;
        foreach (int seat in opponentSeats)
        {
            if (participants[seat] is not null)
                continue;

            participants[seat] = new Participant(Roles[seat],
                randomPlaced ? OpponentKind.Scripted : OpponentKind.Random);
            randomPlaced = true;
        }

        var order = Enumerable.Range(0, Roles.Length).ToArray();
        schedule.Shuffle(order);
        var lineup = order.Select(seat => participants[seat]!).ToArray();
        var usedInstances = new Dictionary<Checkpoint, int>();
        var budget = new MatchActionBudget();
        var players = lineup.Select(participant =>
            new Player(participant.Role, new BudgetedAgent(GetAgent(participant, usedInstances), budget))).ToArray();

        GlobalRnd.SetSeed(schedule.Next());
        PlayerRole? winner = null;
        try
        {
            winner = new GameEngine(players).Play().WinningRole;
        }
        catch (MatchActionLimitException)
        {
            // The engine has no turn limit. Count the capped match as a draw.
        }

        return new MatchResult(candidate, lineup, Array.IndexOf(order, focalSeat), winner, trialSeed);
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
    Checkpoint Candidate, IReadOnlyList<Participant> Lineup, int CandidateSeat, PlayerRole? WinningRole, int Seed)
{
    public bool CandidateWon => WinningRole == TeamFor(Candidate.Role);

    public static PlayerRole TeamFor(PlayerRole role) =>
        role == PlayerRole.Deputy ? PlayerRole.Sheriff : role;
}
