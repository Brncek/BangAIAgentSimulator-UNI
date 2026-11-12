using BangSimulatorLib.Agent;
using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

internal sealed class MatchActionBudget
{
    public const int Limit = 5000;
    private int actions;

    public void CountAction()
    {
        if (++actions > Limit)
            throw new MatchActionLimitException();
    }
}

internal sealed class MatchActionLimitException : Exception;

internal sealed class BudgetedAgent(IAgent agent, MatchActionBudget budget) : IAgent
{
    public AgentAction Step(GameInfo info)
    {
        budget.CountAction();
        return agent.Step(info);
    }

    public void Reset() => agent.Reset();
    public void GameOver(PlayerRole winningRole) => agent.GameOver(winningRole);
    public void SetEval(bool eval) => agent.SetEval(eval);
    public bool HasReward() => agent.HasReward();
    public List<float> GetRewards() => agent.GetRewards();
    public void SetAutoSavePath(string path) => agent.SetAutoSavePath(path);
    public void Load(string path) => agent.Load(path);
    public void SetUseMemory(bool use) => agent.SetUseMemory(use);
}
