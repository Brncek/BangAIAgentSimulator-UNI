using BangSimulatorLib.Agent.Model;
using BangSimulatorLib.Game;

namespace BangSimulatorLib.Agent;

public class TurboScriptedAgent : IAgent
{
    public AgentAction Step(GameInfo info)
    {
        var actions = info.AvanableActions;
        int living = info.GamePlayerLifes.Count(hp => hp > 0);
        bool protectSheriff = info.PlayerRole == PlayerRole.Deputy ||
            (info.PlayerRole == PlayerRole.Renegade && living > 2);

        if (info.Decision == DecisionKind.StorePick)
        {
            var pick = actions.MaxBy(a => a.PlayedCard?.Type switch
            {
                CardBangType.Beer when living > 2 && info.PlayerHelth <= 2 => 10,
                CardBangType.WellsFargo => 9,
                CardBangType.Stagecoach => 8,
                CardBangType.Barrel when !info.CardsOut.Any(c => c.Type == CardBangType.Barrel) => 7,
                CardBangType.Panic or CardBangType.Missed or CardBangType.Bang => 6,
                CardBangType.Dinamite => 0,
                _ => 2
            })!;
            return new AgentAction { PlayedCard = pick.PlayedCard, target = pick.PotencialTargets[0] };
        }
        if (info.Decision is DecisionKind.Defend or DecisionKind.SaveLife)
        {
            bool yieldDuel = info.Decision == DecisionKind.Defend && info.ReactingToCard?.Type == CardBangType.Duel
                && protectSheriff && info.CardFromId == info.ScherifId;
            bool canSurvive = info.Decision != DecisionKind.SaveLife ||
                actions.Count(a => a.PlayedCard?.Type == CardBangType.Beer) >= 1 - info.PlayerHelth;
            var reply = actions.FirstOrDefault(a => a.PlayedCard != null);
            if (!yieldDuel && canSurvive && reply != null)
                return new AgentAction { PlayedCard = reply.PlayedCard, target = reply.PotencialTargets[0] };

            var decline = actions.First(a => a.PlayedCard == null);
            return new AgentAction { target = decline.PotencialTargets[0] };
        }

        var hand = actions.Where(a => a.PlayedCard != null)
            .Select(a => a.PlayedCard!).ToLookup(c => c.Type);
        int bangs = hand[CardBangType.Bang].Count();
        var equippedGun = info.CardsOut.FirstOrDefault(c => c.IsGun());
        int sheriffHp = info.GamePlayerLifes[info.ScherifId];
        // Only public, directed hostile plays are evidence of hostility.
        var suspects = info.DeckMemory.Where(e => e.targetId == info.ScherifId &&
            e.plaied?.Type is CardBangType.Duel or CardBangType.CatBalou or CardBangType.Panic)
            .Select(e => e.pId).ToHashSet();

        int TargetBonus(int target)
        {
            if (target == info.ScherifId)
                return protectSheriff ? -200 : 30;
            int pressure = info.PlayerRole switch
            {
                PlayerRole.Bandit => -18,
                PlayerRole.Sheriff or PlayerRole.Deputy or PlayerRole.Renegade
                    when suspects.Contains(target) => 20,
                _ => 0
            };
            return pressure + (5 - info.GamePlayerLifes[target]) * 3;
        }

        int Score(Card card, int target)
        {
            var type = card.Type;
            if (target == -2)
            {
                // All discards score below ending the turn. When over the
                // hand limit, keep a first defense and discard surplus first.
                int copies = hand[type].Count();
                int keep = type switch
                {
                    CardBangType.Dinamite => 0,
                    _ when card.IsGun() && equippedGun != null => 1,
                    _ when info.CardsOut.Any(c => c.Type == type) => 1,
                    CardBangType.Missed => copies > 1 ? 10 : 35,
                    CardBangType.Beer => living <= 2 ? 0 : copies > 1 ? 18 : 40,
                    CardBangType.Bang => copies > 1 ? 12 : 25,
                    _ => 6
                };
                return -10 - keep;
            }
            if (card.IsGun())
            {
                int gain = GunValue(type, bangs, living) -
                    (equippedGun == null ? 0 : GunValue(equippedGun.Type, bangs, living));
                return gain > 0 ? 48 + gain : -100;
            }
            return type switch
            {
                CardBangType.WellsFargo => 110,
                CardBangType.Stagecoach => 105,
                CardBangType.Beer => info.PlayerHelth <= 2 ? 115 : 83,
                CardBangType.Barrel or CardBangType.Mustang or CardBangType.scope =>
                    info.CardsOut.Any(c => c.Type == type) ? -100 : 65,
                CardBangType.Panic => 66 + TargetBonus(target),
                CardBangType.Bang => 54 + TargetBonus(target),
                CardBangType.Jail => 47 + TargetBonus(target),
                CardBangType.CatBalou => 44 + TargetBonus(target),
                CardBangType.Duel when bangs > 0 => 58 + TargetBonus(target),
                CardBangType.Duel when info.PlayerHelth >= 3 => 25 + TargetBonus(target),
                CardBangType.Gatling or CardBangType.Indians =>
                    protectSheriff ? -100 : info.PlayerRole == PlayerRole.Bandit && sheriffHp == 1 ? 95 : 50,
                CardBangType.Salon when protectSheriff && sheriffHp <= 2 => 90,
                CardBangType.Salon when info.PlayerHelth <= 2 &&
                    (info.PlayerRole == PlayerRole.Sheriff || sheriffHp >= 5) => 68,
                CardBangType.GeneralStore when info.PlayerHelth <= 2 => 8,
                _ => -100
            };
        }

        int best = int.MinValue;
        int ties = 0;
        AgentAction? choice = null;
        foreach (var action in actions)
            foreach (int target in action.PotencialTargets)
            {
                int score = action.PlayedCard == null ? 0 : Score(action.PlayedCard, target);
                if (score < best) continue;
                if (score > best) ties = 0;
                if (++ties > 1 && GlobalRnd.Rnd.Next(ties) != 0) continue;
                best = score;
                choice = new AgentAction { PlayedCard = action.PlayedCard, target = target };
            }
        return choice ?? throw new InvalidOperationException("No legal action offered.");
    }

    private static int GunValue(CardBangType type, int bangs, int living) => type switch
    {
        CardBangType.Vulcanic => bangs >= 2 || living <= 3 ? 9 : 2,
        CardBangType.Winchester => 7,
        CardBangType.Carabine => 6,
        CardBangType.Remington => 5,
        CardBangType.Schofield => 4,
        _ => 0
    };

    public void GameOver(PlayerRole winingRole) { }
    public void Reset() { }
    public void SetEval(bool eval) { }
    public bool HasReward() => false;
    public List<float> GetRewards() => [];
    public void SetAutoSavePath(string pathFolder) { }
    public void Load(string path) { }
}
