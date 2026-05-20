using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result payload for a single combat action resolution.
/// </summary>
public readonly struct CombatActionResult
{
    public readonly CombatActionType ActionType;
    public readonly bool Succeeded;
    public readonly int HpDamage;
    public readonly int BreakDamage;
    public readonly int HealAmount;
    public readonly bool BreakTriggered;
    public readonly bool TargetKilled;
    public readonly string FailureReason;

    public CombatActionResult(
        CombatActionType actionType,
        bool succeeded,
        int hpDamage,
        int breakDamage,
        int healAmount,
        bool breakTriggered,
        bool targetKilled,
        string failureReason = null)
    {
        ActionType = actionType;
        Succeeded = succeeded;
        HpDamage = hpDamage;
        BreakDamage = breakDamage;
        HealAmount = healAmount;
        BreakTriggered = breakTriggered;
        TargetKilled = targetKilled;
        FailureReason = failureReason;
    }
}

/// <summary>
/// Resolves action effects between source and target actor runtimes.
/// </summary>
public class CombatActionResolver
{
    public CombatActionResult ResolvePlayerCommand(CombatActorRuntime source, CombatActorRuntime target, CombatCommandRuntime command)
    {
        if (source == null || target == null || source.IsDead || target.IsDead)
        {
            return new CombatActionResult(command.ActionType, false, 0, 0, 0, false, false, "Invalid actor or dead target.");
        }

        int previousGuard = target.GuardValue;
        int breakDamage = target.ApplyBreakDamage(command.BreakDamage);
        bool breakTriggered = previousGuard > 0 && target.GuardValue <= 0;

        int hpDamage = target.ApplyIncomingDamage(command.HpDamage);
        bool killed = target.IsDead;

        return new CombatActionResult(command.ActionType, true, hpDamage, breakDamage, 0, breakTriggered, killed);
    }

    public CombatActionResult ResolveEnemyIntentSingleTarget(CombatActorRuntime source, CombatActorRuntime target, CombatIntentRuntime intent)
    {
        if (source == null || target == null || source.IsDead || target.IsDead)
        {
            return new CombatActionResult(intent.ActionType, false, 0, 0, 0, false, false, "Invalid actor or dead target.");
        }

        int hpDamage = target.ApplyIncomingDamage(intent.HpDamage);
        return new CombatActionResult(intent.ActionType, true, hpDamage, 0, 0, false, target.IsDead);
    }

    public CombatActionResult ResolveEnemyIntentHealAll(CombatActorRuntime source, IList<CombatActorRuntime> team, CombatIntentRuntime intent)
    {
        if (source == null || source.IsDead || team == null)
        {
            return new CombatActionResult(intent.ActionType, false, 0, 0, 0, false, false, "Invalid heal source/team.");
        }

        int totalHeal = 0;
        for (int i = 0; i < team.Count; i++)
        {
            CombatActorRuntime actor = team[i];
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            int before = actor.CurrentHp;
            actor.Heal(intent.HealAmount);
            totalHeal += Mathf.Max(0, actor.CurrentHp - before);
        }

        return new CombatActionResult(intent.ActionType, true, 0, 0, totalHeal, false, false);
    }

    public CombatActionResult ResolveEnemyIntentAoe(CombatActorRuntime source, IList<CombatActorRuntime> targets, CombatIntentRuntime intent)
    {
        if (source == null || source.IsDead || targets == null)
        {
            return new CombatActionResult(intent.ActionType, false, 0, 0, 0, false, false, "Invalid aoe source/targets.");
        }

        int totalDamage = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            CombatActorRuntime target = targets[i];
            if (target == null || target.IsDead)
            {
                continue;
            }

            totalDamage += target.ApplyIncomingDamage(intent.HpDamage);
        }

        return new CombatActionResult(intent.ActionType, true, totalDamage, 0, 0, false, false);
    }

    public static int ApplyGuardToAllies(IList<CombatActorRuntime> allies, int guardGain)
    {
        if (allies == null || guardGain <= 0)
        {
            return 0;
        }

        int changedCount = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            CombatActorRuntime ally = allies[i];
            if (ally == null || ally.IsDead)
            {
                continue;
            }

            int before = ally.GuardValue;
            ally.AddGuard(guardGain);
            if (ally.GuardValue != before)
            {
                changedCount += 1;
            }
        }

        return changedCount;
    }

    public static int ApplyGuardToActor(CombatActorRuntime actor, int guardGain)
    {
        if (actor == null || actor.IsDead || guardGain <= 0)
        {
            return actor != null ? actor.GuardValue : 0;
        }

        return actor.AddGuard(guardGain);
    }
}
