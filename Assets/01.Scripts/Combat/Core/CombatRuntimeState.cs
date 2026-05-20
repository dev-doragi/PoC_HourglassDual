using System.Collections.Generic;
using UnityEngine;

public enum CombatDifficulty
{
    Standard = 0,
    Hard = 1
}

public enum CombatTimelineSide
{
    Ally = 0,
    Enemy = 1
}

public enum CombatTimelineEntryType
{
    Command = 0,
    Intent = 1
}

public struct CombatCommandRuntime
{
    public string ActionId;
    public string DisplayName;
    public int SourceActorId;
    public int SourceSlotIndex;
    public int TargetActorId;
    public CombatActionType ActionType;
    public int Cost;
    public int Speed;
    public int HpDamage;
    public int BreakDamage;
    public int GuardGain;
    public int HealAmount;
    public CombatTargetType TargetType;
    public bool CanTargetAlly;
    public bool CanTargetEnemy;
    public bool IsAreaAction;
    public CombatActionType FallbackActionType;
    public bool AppliesToAllAllies;
}

public struct CombatIntentRuntime
{
    public string IntentId;
    public string DisplayName;
    public int SourceActorId;
    public int SourceSlotIndex;
    public CombatActionType ActionType;
    public int TargetActorId;
    public int BaseCost;
    public int MinEffectiveCost;
    public int EffectiveCost;
    public int Speed;
    public int HpDamage;
    public int GuardDamage;
    public int HealAmount;
    public CombatTargetType TargetRule;
    public bool IsAoe;
    public CombatActionType FallbackActionType;
}

public struct CombatTimelineEntryRuntime
{
    public int TimelineIndex;
    public CombatTimelineSide Side;
    public CombatTimelineEntryType EntryType;
    public int SourceActorId;
    public int SourceSlotIndex;
    public int TargetActorId;
    public CombatActionType ActionType;
    public int Cost;
    public int Speed;
    public string DisplayName;
    public string Status;
    public bool IsCurrent;
    public bool IsSkipped;
    public bool IsDelayed;
    public bool IsDead;
}

/// <summary>
/// Runtime container for current combat flow state.
/// </summary>
public class CombatRuntimeState
{
    public int TurnIndex;
    public CombatTurnState TurnState;
    public bool IsCombatEnded;

    public int TotalSand;
    public int UpperSand;
    public int LowerSand;
    public int LockedSand;
    public int MinimumFall;
    public int PlayerSand;
    public int PlayerSpend;
    public int EnemySand;

    public int Pressure;
    public CombatDifficulty Difficulty;

    public int KillBonusToken;
    public bool KillBonusGrantedThisRound;
    public bool KillBonusCommandConsumedThisRound;

    public int SelectedAllySlot;
    public int SelectedEnemySlot;
    public int EnemyCostMin;
    public int MaxKillBonusPerRound;
    public bool EnableKillBonus;
    public CombatDifficultyDataSO DifficultyData;

    public readonly List<CombatActorRuntime> Allies = new List<CombatActorRuntime>(3);
    public readonly List<CombatActorRuntime> Enemies = new List<CombatActorRuntime>(3);
    public readonly List<CombatCommandRuntime> QueuedCommands = new List<CombatCommandRuntime>(8);
    public readonly List<CombatIntentRuntime> EnemyIntents = new List<CombatIntentRuntime>(3);
    public readonly List<CombatTimelineEntryRuntime> EnemyOrder = new List<CombatTimelineEntryRuntime>(6);

    public CombatActorRuntime GetActorById(int actorId)
    {
        for (int i = 0; i < Allies.Count; i++)
        {
            if (Allies[i] != null && Allies[i].ActorId == actorId)
            {
                return Allies[i];
            }
        }

        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i] != null && Enemies[i].ActorId == actorId)
            {
                return Enemies[i];
            }
        }

        return null;
    }

    public CombatActorRuntime GetSelectedAlly()
    {
        if (Allies.Count == 0)
        {
            return null;
        }

        int safeSlot = Mathf.Clamp(SelectedAllySlot, 0, Allies.Count - 1);
        return Allies[safeSlot];
    }

    public CombatActorRuntime GetSelectedEnemy()
    {
        if (Enemies.Count == 0)
        {
            return null;
        }

        int safeSlot = Mathf.Clamp(SelectedEnemySlot, 0, Enemies.Count - 1);
        return Enemies[safeSlot];
    }

    public CombatActorRuntime GetFirstAliveEnemy()
    {
        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i] != null && !Enemies[i].IsDead)
            {
                return Enemies[i];
            }
        }

        return null;
    }

    public CombatActorRuntime GetFirstAliveAlly()
    {
        for (int i = 0; i < Allies.Count; i++)
        {
            if (Allies[i] != null && !Allies[i].IsDead)
            {
                return Allies[i];
            }
        }

        return null;
    }

    public int CountAliveAllies()
    {
        int alive = 0;
        for (int i = 0; i < Allies.Count; i++)
        {
            if (Allies[i] != null && !Allies[i].IsDead)
            {
                alive += 1;
            }
        }

        return alive;
    }

    public int CountAliveEnemies()
    {
        int alive = 0;
        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i] != null && !Enemies[i].IsDead)
            {
                alive += 1;
            }
        }

        return alive;
    }

    public void BeginRoundPlanning()
    {
        TurnState = CombatTurnState.PlayerCommand;
        QueuedCommands.Clear();
        EnemyOrder.Clear();
        PlayerSpend = 0;
        EnemySand = 0;
        KillBonusGrantedThisRound = false;
        KillBonusCommandConsumedThisRound = false;

        for (int i = 0; i < Allies.Count; i++)
        {
            Allies[i]?.ResetRoundFlags();
        }

        for (int i = 0; i < Enemies.Count; i++)
        {
            CombatActorRuntime enemy = Enemies[i];
            if (enemy == null)
            {
                continue;
            }

            enemy.ResetRoundFlags();
            enemy.SkipCurrentAction = enemy.BreakSkipCount > 0;
        }
    }
}
