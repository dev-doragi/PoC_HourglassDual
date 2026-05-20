using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime state for a single combat actor in 3v3 battle.
/// </summary>
public class CombatActorRuntime
{
    public int ActorId;
    public int SlotIndex;
    public string DisplayName;
    public CombatActorType ActorType;
    public CombatRoleType RoleType;

    public int MaxHp;
    public int CurrentHp;
    public int MaxGuard;
    public int GuardValue;
    public int SpeedBase;

    public bool IsSelected;

    // Legacy flags kept for UI compatibility.
    public bool SkipCurrentAction;
    public bool SkipNextAction;
    public bool HasActedThisRound;
    public int BreakSkipCount;
    public float BreakDelayRatio;
    public float BaseActionValue;
    public float CurrentActionValue;

    public readonly List<CombatActionDataSO> ActionList = new List<CombatActionDataSO>(8);
    public readonly List<EnemyIntentDataSO> EnemyIntentList = new List<EnemyIntentDataSO>(8);

    public bool IsDead => CurrentHp <= 0;
    public bool IsBroken => !IsDead && GuardValue <= 0;

    public void ResetRoundFlags()
    {
        HasActedThisRound = false;
        SkipCurrentAction = BreakSkipCount > 0;
        SkipNextAction = false;
    }

    public int ApplyIncomingDamage(int hpDamage)
    {
        int safeDamage = Mathf.Max(0, hpDamage);
        if (safeDamage == 0 || IsDead)
        {
            return 0;
        }

        CurrentHp = Mathf.Max(0, CurrentHp - safeDamage);
        return safeDamage;
    }

    public int ApplyBreakDamage(int breakDamage)
    {
        int safeBreak = Mathf.Max(0, breakDamage);
        if (safeBreak == 0 || IsDead)
        {
            return 0;
        }

        int before = GuardValue;
        GuardValue = Mathf.Max(0, GuardValue - safeBreak);
        return Mathf.Max(0, before - GuardValue);
    }

    public int AddGuard(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0 || IsDead)
        {
            return GuardValue;
        }

        GuardValue = Mathf.Clamp(GuardValue + safeAmount, 0, Mathf.Max(1, MaxGuard));
        return GuardValue;
    }

    public int Heal(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0 || IsDead)
        {
            return CurrentHp;
        }

        CurrentHp = Mathf.Clamp(CurrentHp + safeAmount, 0, Mathf.Max(1, MaxHp));
        return CurrentHp;
    }

    public static CombatActorRuntime Create(
        CombatActorDataSO data,
        CombatActorType expectedTeamType,
        int fallbackSlotIndex)
    {
        if (data == null)
        {
            return null;
        }

        int safeHp = Mathf.Max(1, data.maxHP);
        int safeGuard = Mathf.Max(0, data.maxGuard);

        CombatActorRuntime runtime = new CombatActorRuntime
        {
            ActorId = data.actorId,
            SlotIndex = Mathf.Max(0, data.slotIndex >= 0 ? data.slotIndex : fallbackSlotIndex),
            DisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.name : data.displayName,
            ActorType = data.teamType == CombatActorType.None ? expectedTeamType : data.teamType,
            RoleType = data.roleType,
            MaxHp = safeHp,
            CurrentHp = safeHp,
            MaxGuard = safeGuard,
            GuardValue = safeGuard,
            SpeedBase = Mathf.Max(0, data.speedBase),
            IsSelected = false,
            SkipCurrentAction = false,
            SkipNextAction = false,
            HasActedThisRound = false,
            BreakSkipCount = 0,
            BreakDelayRatio = 0.25f,
            BaseActionValue = data.speedBase > 0 ? (10000f / data.speedBase) : 10000f,
            CurrentActionValue = data.speedBase > 0 ? (10000f / data.speedBase) : 10000f
        };

        if (data.actionList != null)
        {
            for (int i = 0; i < data.actionList.Length; i++)
            {
                if (data.actionList[i] != null)
                {
                    runtime.ActionList.Add(data.actionList[i]);
                }
            }
        }

        if (data.enemyIntentList != null)
        {
            for (int i = 0; i < data.enemyIntentList.Length; i++)
            {
                if (data.enemyIntentList[i] != null)
                {
                    runtime.EnemyIntentList.Add(data.enemyIntentList[i]);
                }
            }
        }

        return runtime;
    }
}
