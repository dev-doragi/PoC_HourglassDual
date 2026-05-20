using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-70)]
public class HourglassCombatManager : Singleton<HourglassCombatManager>
{
    [SerializeField] private HourglassCombatConfigSO _config;
    [SerializeField] private CombatActionDataSO[] _globalActionCatalog;
    [SerializeField] private float _flipDuration = 0.45f;
    [SerializeField] private float _phaseStepDelay = 0.12f;
    [SerializeField] private float _enemyActionStepDelay = 0.2f;

    private readonly CombatActionResolver _actionResolver = new CombatActionResolver();
    private readonly CombatTurnProcessor _turnProcessor = new CombatTurnProcessor();
    private readonly Dictionary<CombatActionType, CombatActionDataSO> _actionByType = new Dictionary<CombatActionType, CombatActionDataSO>();
    private int? _nextCombatPlayerStartHpOverride;
    private Coroutine _roundResolveRoutine;

    public CombatRuntimeState RuntimeState { get; private set; }
    public float FlipDuration => Mathf.Max(0f, _flipDuration);

    public CombatActorDataSO PlayerData => RuntimeState != null && RuntimeState.Allies.Count > 0 ? ToActorData(RuntimeState.Allies[0].ActorId, CombatActorType.Ally) : null;
    public CombatActorDataSO EnemyData => RuntimeState != null && RuntimeState.Enemies.Count > 0 ? ToActorData(RuntimeState.Enemies[0].ActorId, CombatActorType.Enemy) : null;

    protected override void OnBootstrap()
    {
        InitializeRuntime();
    }

    public void SetNextCombatPlayerStartHpOverride(int playerHp)
    {
        _nextCombatPlayerStartHpOverride = Mathf.Max(0, playerHp);
    }

    public void ConfigureCombatActors(CombatActorDataSO playerData, CombatActorDataSO enemyData)
    {
        if (_config == null)
        {
            Debug.LogError("[Combat] Cannot configure actors because config is null.");
            return;
        }

        if (_config.allyPartyActors == null || _config.allyPartyActors.Length < 3)
        {
            _config.allyPartyActors = new CombatActorDataSO[3];
        }

        if (_config.enemyPartyActors == null || _config.enemyPartyActors.Length < 3)
        {
            _config.enemyPartyActors = new CombatActorDataSO[3];
        }

        if (playerData != null)
        {
            _config.allyPartyActors[0] = playerData;
        }

        if (enemyData != null)
        {
            _config.enemyPartyActors[0] = enemyData;
        }
    }

    public void ConfigureCombatParties(CombatActorDataSO[] allyParty, CombatActorDataSO[] enemyParty)
    {
        if (_config == null)
        {
            Debug.LogError("[Combat] Cannot configure parties because config is null.");
            return;
        }

        if (allyParty == null || allyParty.Length < 3 || enemyParty == null || enemyParty.Length < 3)
        {
            Debug.LogError("[Combat] ConfigureCombatParties requires 3 allies and 3 enemies.");
            return;
        }

        if (_config.allyPartyActors == null || _config.allyPartyActors.Length < 3)
        {
            _config.allyPartyActors = new CombatActorDataSO[3];
        }

        if (_config.enemyPartyActors == null || _config.enemyPartyActors.Length < 3)
        {
            _config.enemyPartyActors = new CombatActorDataSO[3];
        }

        for (int i = 0; i < 3; i++)
        {
            _config.allyPartyActors[i] = allyParty[i];
            _config.enemyPartyActors[i] = enemyParty[i];
        }
    }

    public void StartCombat()
    {
        if (!InitializeRuntime())
        {
            return;
        }

        ApplyNextCombatPlayerHpOverrideIfNeeded();
        BeginRound(1);
    }

    public void RequestStrike() => RequestActionAtQuickSlot(0);
    public void RequestPierce() => RequestActionAtQuickSlot(1);
    public void RequestHex() => RequestActionAtQuickSlot(2);
    public void RequestActionForSelectedAlly(CombatActionDataSO actionData)
    {
        if (actionData == null)
        {
            return;
        }

        TryExecuteActionForSelectedAlly(actionData, -1, out _);
    }

    public bool TryExecuteActionForSelectedAlly(CombatActionDataSO actionData, int targetActorId, out string reason)
    {
        reason = null;
        if (!CanQueueActionForSelectedAlly(actionData, out reason))
        {
            return false;
        }

        CombatActorRuntime source = RuntimeState.GetSelectedAlly();
        if (source == null || source.IsDead)
        {
            reason = "NoAliveSource";
            return false;
        }

        bool useBonusAction = source.HasActedThisRound
            && RuntimeState.EnableKillBonus
            && RuntimeState.KillBonusToken > 0
            && !RuntimeState.KillBonusCommandConsumedThisRound;
        if (useBonusAction)
        {
            RuntimeState.KillBonusCommandConsumedThisRound = true;
        }

        CombatCommandRuntime command = CreateRuntimeCommand(source, actionData);
        if (RequiresEnemyTarget(actionData))
        {
            CombatActorRuntime target = targetActorId > 0 ? RuntimeState.GetActorById(targetActorId) : RuntimeState.GetSelectedEnemy();
            if (target == null || target.IsDead || target.ActorType != CombatActorType.Enemy)
            {
                reason = "TargetRequired";
                return false;
            }

            command.TargetActorId = target.ActorId;
            RuntimeState.SelectedEnemySlot = target.SlotIndex;
        }

        int unlockedSand = Mathf.Max(1, RuntimeState.TotalSand - RuntimeState.LockedSand);
        int cost = Mathf.Clamp(command.Cost, 0, RuntimeState.UpperSand);
        RuntimeState.UpperSand = Mathf.Clamp(RuntimeState.UpperSand - cost, 0, unlockedSand);
        RuntimeState.LowerSand = Mathf.Clamp(RuntimeState.LowerSand + cost, 0, unlockedSand);
        RuntimeState.PlayerSpend = Mathf.Clamp(RuntimeState.PlayerSand - RuntimeState.UpperSand, 0, RuntimeState.PlayerSand);
        EventBus.Instance.Publish(new CombatCommandQueuedEvent(command, RuntimeState.UpperSand, RuntimeState.LowerSand, CreateSnapshot()));

        CombatActionResult result = ApplyCommand(command, source);
        source.HasActedThisRound = true;
        TryAutoSelectNextCommandableAlly(source.SlotIndex);

        string message = result.Succeeded ? "Resolved" : (result.FailureReason ?? "Failed");
        EventBus.Instance.Publish(new CombatAllyCommandResolvedEvent(command, result.Succeeded, message, CreateSnapshot()));
        Debug.Log($"[Combat] AllyCommandResolved | {source.DisplayName} {command.ActionType} {message}");

        EvaluateCombatEnd();
        return result.Succeeded;
    }

    public void RequestGuard()
    {
        if (!CanAcceptPlayerInput())
        {
            return;
        }

        SelectEnemyBySlot(FindNextAliveEnemySlot(RuntimeState.SelectedEnemySlot));
    }

    public void RequestEndTurn()
    {
        if (!CanAcceptPlayerInput())
        {
            return;
        }
        if (_roundResolveRoutine == null)
        {
            _roundResolveRoutine = StartCoroutine(ConfirmAndRunRoundRoutine());
        }
    }

    public bool SelectAllyBySlot(int slotIndex)
    {
        if (RuntimeState == null || RuntimeState.Allies.Count == 0)
        {
            return false;
        }

        RuntimeState.SelectedAllySlot = Mathf.Clamp(slotIndex, 0, RuntimeState.Allies.Count - 1);
        PublishActorSelected(CombatActorType.Ally, RuntimeState.SelectedAllySlot);
        return true;
    }

    public bool SelectEnemyBySlot(int slotIndex)
    {
        if (RuntimeState == null || RuntimeState.Enemies.Count == 0)
        {
            return false;
        }

        RuntimeState.SelectedEnemySlot = Mathf.Clamp(slotIndex, 0, RuntimeState.Enemies.Count - 1);
        PublishActorSelected(CombatActorType.Enemy, RuntimeState.SelectedEnemySlot);
        return true;
    }

    public CombatActionDataSO[] GetSelectedAllyActions()
    {
        CombatActorRuntime ally = RuntimeState != null ? RuntimeState.GetSelectedAlly() : null;
        if (ally == null || ally.ActionList.Count == 0)
        {
            return Array.Empty<CombatActionDataSO>();
        }

        return ally.ActionList.ToArray();
    }

    public bool CanQueueActionForSelectedAlly(CombatActionDataSO actionData, out string reason)
    {
        reason = null;
        if (RuntimeState == null)
        {
            reason = "NoRuntime";
            return false;
        }

        if (!CanAcceptPlayerInput())
        {
            reason = "NotPlayerCommand";
            return false;
        }

        CombatActorRuntime source = RuntimeState.GetSelectedAlly();
        if (source == null || source.IsDead)
        {
            reason = "NoAliveSource";
            return false;
        }

        bool canUseBonus = RuntimeState.EnableKillBonus
            && RuntimeState.KillBonusToken > 0
            && !RuntimeState.KillBonusCommandConsumedThisRound;
        if (source.HasActedThisRound && !canUseBonus)
        {
            reason = "AlreadyActed";
            return false;
        }

        if (actionData == null)
        {
            reason = "ActionNull";
            return false;
        }

        int remainingUpper = Mathf.Max(0, RuntimeState.UpperSand);
        if (remainingUpper < Mathf.Max(0, actionData.baseCost))
        {
            reason = "NotEnoughUpperSand";
            return false;
        }

        if (RequiresEnemyTarget(actionData))
        {
            CombatActorRuntime enemy = RuntimeState.GetFirstAliveEnemy();
            if (enemy == null)
            {
                reason = "TargetRequired";
                return false;
            }
        }

        return true;
    }

    public bool HasAnyCommandableAlly()
    {
        if (RuntimeState == null || !CanAcceptPlayerInput())
        {
            return false;
        }

        for (int i = 0; i < RuntimeState.Allies.Count; i++)
        {
            if (CanActorTakeAnyAction(RuntimeState.Allies[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool InitializeRuntime()
    {
        RuntimeState = null;
        if (!ValidateConfig())
        {
            return false;
        }

        BuildActionLookup();

        CombatDifficultyDataSO difficulty = _config.selectedDifficulty;
        int totalSand = Mathf.Max(1, _config.totalSand);
        int lockedSand = Mathf.Clamp(_config.lockedSand, 0, Mathf.Max(0, totalSand - 1));
        int unlockedSand = Mathf.Max(1, totalSand - lockedSand);
        int startSand = Mathf.Clamp(Mathf.Max(0, difficulty.playerStartSand), 0, unlockedSand);

        RuntimeState = new CombatRuntimeState
        {
            TurnIndex = 0,
            TurnState = CombatTurnState.None,
            IsCombatEnded = false,
            TotalSand = totalSand,
            LockedSand = lockedSand,
            MinimumFall = Mathf.Max(0, difficulty.minimumFall),
            PlayerSand = startSand,
            UpperSand = startSand,
            LowerSand = unlockedSand - startSand,
            PlayerSpend = 0,
            EnemySand = 0,
            Pressure = 0,
            Difficulty = string.Equals(difficulty.difficultyName, "Hard", StringComparison.OrdinalIgnoreCase)
                ? CombatDifficulty.Hard
                : CombatDifficulty.Standard,
            DifficultyData = difficulty,
            EnemyCostMin = Mathf.Max(0, difficulty.enemyCostMin),
            EnableKillBonus = _config.enableKillBonus,
            MaxKillBonusPerRound = Mathf.Max(0, _config.maxKillBonusPerRound),
            KillBonusToken = 0,
            KillBonusGrantedThisRound = false,
            KillBonusCommandConsumedThisRound = false,
            SelectedAllySlot = Mathf.Clamp(_config.defaultSelectedAllyIndex, 0, 2),
            SelectedEnemySlot = Mathf.Clamp(_config.defaultSelectedEnemyIndex, 0, 2)
        };

        BuildParty(RuntimeState.Allies, _config.allyPartyActors, CombatActorType.Ally);
        BuildParty(RuntimeState.Enemies, _config.enemyPartyActors, CombatActorType.Enemy);
        InitializeEnemyActionValue();
        EnsureSelectionsAreAlive();

        return true;
    }

    private bool ValidateConfig()
    {
        if (_config == null)
        {
            Debug.LogError("[Combat] HourglassCombatConfigSO missing.");
            return false;
        }

        if (_config.selectedDifficulty == null)
        {
            Debug.LogError("[Combat] selectedDifficulty is null.");
            return false;
        }

        if (_config.allyPartyActors == null || _config.allyPartyActors.Length < 3)
        {
            Debug.LogError("[Combat] allyPartyActors must contain 3 actors.");
            return false;
        }

        if (_config.enemyPartyActors == null || _config.enemyPartyActors.Length < 3)
        {
            Debug.LogError("[Combat] enemyPartyActors must contain 3 actors.");
            return false;
        }

        for (int i = 0; i < 3; i++)
        {
            if (_config.allyPartyActors[i] == null)
            {
                Debug.LogError($"[Combat] allyPartyActors[{i}] is null.");
                return false;
            }

            if (_config.enemyPartyActors[i] == null)
            {
                Debug.LogError($"[Combat] enemyPartyActors[{i}] is null.");
                return false;
            }
        }

        return true;
    }

    private void BuildActionLookup()
    {
        _actionByType.Clear();
        if (_globalActionCatalog != null)
        {
            for (int i = 0; i < _globalActionCatalog.Length; i++)
            {
                CombatActionDataSO action = _globalActionCatalog[i];
                if (action == null || action.actionType == CombatActionType.None)
                {
                    continue;
                }

                _actionByType[action.actionType] = action;
            }
        }

        AddActorActionsToLookup(_config.allyPartyActors);
        AddActorActionsToLookup(_config.enemyPartyActors);
    }

    private void AddActorActionsToLookup(CombatActorDataSO[] actors)
    {
        if (actors == null)
        {
            return;
        }

        for (int i = 0; i < actors.Length; i++)
        {
            CombatActorDataSO actor = actors[i];
            if (actor == null || actor.actionList == null)
            {
                continue;
            }

            for (int j = 0; j < actor.actionList.Length; j++)
            {
                CombatActionDataSO action = actor.actionList[j];
                if (action == null || action.actionType == CombatActionType.None)
                {
                    continue;
                }

                _actionByType[action.actionType] = action;
            }
        }
    }

    private void BuildParty(List<CombatActorRuntime> target, CombatActorDataSO[] source, CombatActorType expectedType)
    {
        target.Clear();
        int count = Mathf.Min(3, source.Length);
        for (int i = 0; i < count; i++)
        {
            CombatActorRuntime runtime = CombatActorRuntime.Create(source[i], expectedType, i);
            if (runtime != null)
            {
                runtime.SlotIndex = i;
                target.Add(runtime);
            }
        }
    }

    private void InitializeEnemyActionValue()
    {
        for (int i = 0; i < RuntimeState.Enemies.Count; i++)
        {
            CombatActorRuntime enemy = RuntimeState.Enemies[i];
            if (enemy == null)
            {
                continue;
            }

            int speed = Mathf.Max(1, enemy.SpeedBase);
            enemy.BaseActionValue = 10000f / speed;
            enemy.CurrentActionValue = enemy.BaseActionValue;
            if (enemy.BreakDelayRatio <= 0f)
            {
                enemy.BreakDelayRatio = 0.25f;
            }
        }
    }

    private void ApplyNextCombatPlayerHpOverrideIfNeeded()
    {
        if (!_nextCombatPlayerStartHpOverride.HasValue || RuntimeState == null || RuntimeState.Allies.Count == 0)
        {
            _nextCombatPlayerStartHpOverride = null;
            return;
        }

        RuntimeState.Allies[0].CurrentHp = Mathf.Clamp(_nextCombatPlayerStartHpOverride.Value, 0, RuntimeState.Allies[0].MaxHp);
        _nextCombatPlayerStartHpOverride = null;
    }

    private void BeginRound(int round)
    {
        if (RuntimeState == null || RuntimeState.IsCombatEnded)
        {
            return;
        }

        RuntimeState.TurnState = CombatTurnState.RoundStart;
        RuntimeState.TurnIndex = Mathf.Max(1, round);

        int previousPressure = RuntimeState.Pressure;
        RuntimeState.Pressure = CombatTurnProcessor.ComputePressure(RuntimeState.DifficultyData, RuntimeState.TurnIndex);
        if (previousPressure != RuntimeState.Pressure)
        {
            EventBus.Instance.Publish(new CombatPressureChangedEvent(previousPressure, RuntimeState.Pressure, CreateSnapshot()));
        }

        int unlockedSand = Mathf.Max(1, RuntimeState.TotalSand - RuntimeState.LockedSand);
        RuntimeState.UpperSand = Mathf.Clamp(RuntimeState.PlayerSand, 0, unlockedSand);
        RuntimeState.LowerSand = Mathf.Clamp(unlockedSand - RuntimeState.UpperSand, 0, unlockedSand);

        RuntimeState.EnemyIntents.Clear();
        RuntimeState.BeginRoundPlanning();
        EnsureSelectionsAreAlive();
        RuntimeState.SelectedAllySlot = FindFirstAliveSlot(RuntimeState.Allies);
        RuntimeState.SelectedEnemySlot = FindFirstAliveSlot(RuntimeState.Enemies);

        BuildEnemyIntentsForRound();
        RebuildEnemyOrder("RoundStart");

        EventBus.Instance.Publish(new CombatRoundStartedEvent(CreateSnapshot()));
        Debug.Log($"[Combat] RoundStart R{RuntimeState.TurnIndex} | Pressure:{RuntimeState.Pressure}");
        DebugTeamStatus("RoundStart");
    }

    private void BuildEnemyIntentsForRound()
    {
        for (int i = 0; i < RuntimeState.Enemies.Count; i++)
        {
            CombatActorRuntime enemy = RuntimeState.Enemies[i];
            if (enemy == null || enemy.IsDead || enemy.EnemyIntentList.Count == 0)
            {
                continue;
            }

            int index = (RuntimeState.TurnIndex - 1) % enemy.EnemyIntentList.Count;
            EnemyIntentDataSO intentData = enemy.EnemyIntentList[index];
            if (intentData == null)
            {
                continue;
            }

            CombatIntentRuntime runtimeIntent = CreateRuntimeIntent(enemy, intentData);
            RuntimeState.EnemyIntents.Add(runtimeIntent);
            EventBus.Instance.Publish(new CombatIntentShownEvent(ToIntentSnapshot(runtimeIntent), CreateSnapshot()));
            Debug.Log($"[Combat] EnemyIntentShown | {enemy.DisplayName} {runtimeIntent.ActionType} c{runtimeIntent.EffectiveCost}/{runtimeIntent.BaseCost}");
        }
    }

    private CombatIntentRuntime CreateRuntimeIntent(CombatActorRuntime source, EnemyIntentDataSO data)
    {
        int targetId = ResolveTargetIdForIntent(data.targetRule, source);
        int minCost = Mathf.Max(data.minEffectiveCost, RuntimeState.EnemyCostMin);
        int effectiveCost = Mathf.Max(minCost, Mathf.Max(0, data.baseCost) - RuntimeState.Pressure);

        int speedFromIntent = Mathf.Max(0, data.speed);
        if (speedFromIntent > 0)
        {
            float adjusted = 10000f / speedFromIntent;
            source.BaseActionValue = Mathf.Max(1f, adjusted);
            if (source.CurrentActionValue < 1f)
            {
                source.CurrentActionValue = source.BaseActionValue;
            }
        }

        return new CombatIntentRuntime
        {
            IntentId = string.IsNullOrWhiteSpace(data.intentId) ? data.name : data.intentId,
            DisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.name : data.displayName,
            SourceActorId = source.ActorId,
            SourceSlotIndex = source.SlotIndex,
            ActionType = data.actionType,
            TargetActorId = targetId,
            BaseCost = Mathf.Max(0, data.baseCost),
            MinEffectiveCost = minCost,
            EffectiveCost = effectiveCost,
            Speed = speedFromIntent,
            HpDamage = Mathf.Max(0, data.hpDamage),
            GuardDamage = Mathf.Max(0, data.guardDamage),
            HealAmount = Mathf.Max(0, data.healAmount),
            TargetRule = data.targetRule,
            IsAoe = data.isAreaAction,
            FallbackActionType = data.fallbackActionType
        };
    }

    private int ResolveTargetIdForIntent(CombatTargetType targetType, CombatActorRuntime source)
    {
        if (targetType == CombatTargetType.Self)
        {
            return source != null ? source.ActorId : -1;
        }

        if (targetType == CombatTargetType.SingleAlly)
        {
            CombatActorRuntime allyEnemyTeam = RuntimeState.GetFirstAliveEnemy();
            return allyEnemyTeam != null ? allyEnemyTeam.ActorId : -1;
        }

        CombatActorRuntime target = RuntimeState.GetFirstAliveAlly();
        return target != null ? target.ActorId : -1;
    }

    private bool CanAcceptPlayerInput()
    {
        return RuntimeState != null
            && !RuntimeState.IsCombatEnded
            && RuntimeState.TurnState == CombatTurnState.PlayerCommand;
    }

    private void RequestActionAtQuickSlot(int actionIndex)
    {
        if (!CanAcceptPlayerInput())
        {
            return;
        }

        CombatActorRuntime source = RuntimeState.GetSelectedAlly();
        if (source == null || source.IsDead || actionIndex < 0 || source.ActionList.Count <= actionIndex)
        {
            return;
        }

        CombatActionDataSO actionData = source.ActionList[actionIndex];
        if (!TryExecuteActionForSelectedAlly(actionData, -1, out string reason))
        {
            Debug.LogWarning($"[Combat] Action blocked: {reason}");
        }
    }

    private void QueueOrReplaceCommand(CombatCommandRuntime command)
    {
        int existingIndex = FindFirstQueuedCommandIndex(command.SourceActorId);
        if (existingIndex >= 0)
        {
            bool canUseBonus = RuntimeState.EnableKillBonus
                && RuntimeState.KillBonusToken > 0
                && !RuntimeState.KillBonusCommandConsumedThisRound
                && RuntimeState.QueuedCommands.Count >= RuntimeState.CountAliveAllies();

            if (canUseBonus)
            {
                RuntimeState.KillBonusCommandConsumedThisRound = true;
                RuntimeState.QueuedCommands.Add(command);
                return;
            }

            RuntimeState.QueuedCommands[existingIndex] = command;
            return;
        }

        RuntimeState.QueuedCommands.Add(command);
    }

    private CombatCommandRuntime CreateRuntimeCommand(CombatActorRuntime source, CombatActionDataSO actionData)
    {
        return new CombatCommandRuntime
        {
            ActionId = string.IsNullOrWhiteSpace(actionData.actionId) ? actionData.name : actionData.actionId,
            DisplayName = string.IsNullOrWhiteSpace(actionData.displayName) ? actionData.name : actionData.displayName,
            SourceActorId = source.ActorId,
            SourceSlotIndex = source.SlotIndex,
            TargetActorId = ResolveTargetIdForAction(actionData, source),
            ActionType = actionData.actionType,
            Cost = Mathf.Max(0, actionData.baseCost),
            Speed = Mathf.Max(0, actionData.speed),
            HpDamage = Mathf.Max(0, actionData.hpDamage),
            BreakDamage = Mathf.Max(0, actionData.guardDamage),
            GuardGain = Mathf.Max(0, actionData.guardGain),
            HealAmount = Mathf.Max(0, actionData.healAmount),
            TargetType = actionData.targetType,
            CanTargetAlly = actionData.canTargetAlly,
            CanTargetEnemy = actionData.canTargetEnemy,
            IsAreaAction = actionData.isAreaAction,
            FallbackActionType = actionData.fallbackActionType,
            AppliesToAllAllies = actionData.targetType == CombatTargetType.AllAllies
        };
    }

    private int ResolveTargetIdForAction(CombatActionDataSO actionData, CombatActorRuntime source)
    {
        if (actionData == null)
        {
            return -1;
        }

        if (actionData.targetType == CombatTargetType.Self)
        {
            return source.ActorId;
        }

        if (actionData.targetType == CombatTargetType.SingleAlly)
        {
            CombatActorRuntime ally = RuntimeState.GetSelectedAlly() ?? RuntimeState.GetFirstAliveAlly();
            return ally != null ? ally.ActorId : -1;
        }

        if (actionData.targetType == CombatTargetType.SingleEnemy)
        {
            CombatActorRuntime enemy = RuntimeState.GetSelectedEnemy() ?? RuntimeState.GetFirstAliveEnemy();
            return enemy != null ? enemy.ActorId : -1;
        }

        return -1;
    }

    private void RecomputePlayerSpend()
    {
        int spend = 0;
        for (int i = 0; i < RuntimeState.QueuedCommands.Count; i++)
        {
            spend += Mathf.Max(0, RuntimeState.QueuedCommands[i].Cost);
        }

        RuntimeState.PlayerSpend = Mathf.Max(0, spend);
    }

    private IEnumerator ConfirmAndRunRoundRoutine()
    {
        RuntimeState.TurnState = CombatTurnState.PlayerResolving;
        RuntimeState.PlayerSpend = Mathf.Clamp(RuntimeState.PlayerSand - RuntimeState.UpperSand, 0, RuntimeState.PlayerSand);
        EventBus.Instance.Publish(new CombatCommandConfirmedEvent(RuntimeState.PlayerSpend, CreateSnapshot()));
        Debug.Log($"[Combat] CommandConfirmed | Spend:{RuntimeState.PlayerSpend}");

        if (RuntimeState.KillBonusCommandConsumedThisRound && RuntimeState.KillBonusToken > 0)
        {
            RuntimeState.KillBonusToken = Mathf.Max(0, RuntimeState.KillBonusToken - 1);
        }

        if (_phaseStepDelay > 0f) yield return new WaitForSeconds(_phaseStepDelay);
        EvaluateCombatEnd();
        if (RuntimeState.IsCombatEnded)
        {
            _roundResolveRoutine = null;
            yield break;
        }

        ApplyMinimumFallAndFlip();
        yield return new WaitForSeconds(Mathf.Max(_phaseStepDelay, _flipDuration));
        EvaluateCombatEnd();
        if (RuntimeState.IsCombatEnded)
        {
            _roundResolveRoutine = null;
            yield break;
        }

        yield return StartCoroutine(ResolveEnemyTurnRoutine());
        if (_phaseStepDelay > 0f) yield return new WaitForSeconds(_phaseStepDelay);
        EvaluateCombatEnd();
        if (RuntimeState.IsCombatEnded)
        {
            _roundResolveRoutine = null;
            yield break;
        }

        PublishTurnSwapFlipOnly();
        yield return new WaitForSeconds(Mathf.Max(_phaseStepDelay, _flipDuration));

        BeginRound(RuntimeState.TurnIndex + 1);
        _roundResolveRoutine = null;
    }

    private void MovePlayerSpendFromUpperToLower()
    {
        int unlockedSand = Mathf.Max(1, RuntimeState.TotalSand - RuntimeState.LockedSand);
        int spend = Mathf.Clamp(RuntimeState.PlayerSpend, 0, RuntimeState.UpperSand);
        RuntimeState.UpperSand = Mathf.Clamp(RuntimeState.UpperSand - spend, 0, unlockedSand);
        RuntimeState.LowerSand = Mathf.Clamp(RuntimeState.LowerSand + spend, 0, unlockedSand);
    }

    private void ResolveAllyCommands()
    {
        List<CombatCommandRuntime> executionOrder = BuildAllyExecutionOrder();
        for (int i = 0; i < executionOrder.Count; i++)
        {
            CombatCommandRuntime command = executionOrder[i];
            CombatActorRuntime source = RuntimeState.GetActorById(command.SourceActorId);
            if (source == null || source.IsDead)
            {
                EventBus.Instance.Publish(new CombatAllyCommandResolvedEvent(command, false, "Dead", CreateSnapshot()));
                continue;
            }

            CombatActionResult result = ApplyCommand(command, source);
            string message = result.Succeeded ? "Resolved" : (result.FailureReason ?? "Failed");
            EventBus.Instance.Publish(new CombatAllyCommandResolvedEvent(command, result.Succeeded, message, CreateSnapshot()));
            Debug.Log($"[Combat] AllyCommandResolved | {source.DisplayName} {command.ActionType} {message}");

            EvaluateCombatEnd();
            if (RuntimeState.IsCombatEnded)
            {
                return;
            }
        }
    }

    private List<CombatCommandRuntime> BuildAllyExecutionOrder()
    {
        List<CombatCommandRuntime> ordered = new List<CombatCommandRuntime>(RuntimeState.QueuedCommands.Count);
        for (int slot = 0; slot < RuntimeState.Allies.Count; slot++)
        {
            for (int i = 0; i < RuntimeState.QueuedCommands.Count; i++)
            {
                CombatCommandRuntime command = RuntimeState.QueuedCommands[i];
                if (command.SourceSlotIndex == slot)
                {
                    ordered.Add(command);
                }
            }
        }

        return ordered;
    }

    private CombatActionResult ApplyCommand(CombatCommandRuntime command, CombatActorRuntime source)
    {
        if (command.TargetType == CombatTargetType.AllAllies)
        {
            for (int i = 0; i < RuntimeState.Allies.Count; i++)
            {
                CombatActorRuntime ally = RuntimeState.Allies[i];
                if (ally == null || ally.IsDead)
                {
                    continue;
                }

                if (command.HealAmount > 0)
                {
                    ally.Heal(command.HealAmount);
                }
            }

            return new CombatActionResult(command.ActionType, true, 0, 0, 0, false, false);
        }

        if (command.TargetType == CombatTargetType.Self || command.TargetType == CombatTargetType.SingleAlly)
        {
            CombatActorRuntime allyTarget = RuntimeState.GetActorById(command.TargetActorId) ?? RuntimeState.GetFirstAliveAlly();
            if (allyTarget == null || allyTarget.IsDead)
            {
                return new CombatActionResult(command.ActionType, false, 0, 0, 0, false, false, "NoAliveAlly");
            }

            if (command.HealAmount > 0)
            {
                allyTarget.Heal(command.HealAmount);
            }

            return new CombatActionResult(command.ActionType, true, 0, 0, 0, false, false);
        }

        CombatActorRuntime enemyTarget = RuntimeState.GetActorById(command.TargetActorId);
        if (enemyTarget == null || enemyTarget.IsDead || enemyTarget.ActorType != CombatActorType.Enemy)
        {
            enemyTarget = RuntimeState.GetFirstAliveEnemy();
        }

        if (enemyTarget == null || enemyTarget.IsDead)
        {
            return new CombatActionResult(command.ActionType, false, 0, 0, 0, false, false, "NoAliveEnemy");
        }

        int beforeGuardValue = enemyTarget.GuardValue;
        CombatActionResult result = _actionResolver.ResolvePlayerCommand(source, enemyTarget, command);
        PublishGuardChangedIfNeeded(enemyTarget, beforeGuardValue, enemyTarget.GuardValue);
        if (result.HpDamage > 0)
        {
            EventBus.Instance.Publish(new CombatActorDamagedEvent(enemyTarget.ActorId, result.HpDamage, CreateSnapshot()));
        }

        if (result.BreakTriggered)
        {
            HandleEnemyBroken(enemyTarget);
        }

        if (result.TargetKilled)
        {
            HandleActorKilled(enemyTarget, source);
        }

        return result;
    }

    private void ApplyMinimumFallAndFlip()
    {
        CombatTurnProcessor.MinimumFallResult minimumFallResult = _turnProcessor.ApplyMinimumFall(RuntimeState);
        EventBus.Instance.Publish(new CombatMinimumFallAppliedEvent(
            minimumFallResult.ForcedFallAmount,
            RuntimeState.MinimumFall,
            minimumFallResult.UpperAfter,
            minimumFallResult.LowerAfter,
            CreateSnapshot()));

        int previousEnemySand = Mathf.Max(0, RuntimeState.EnemySand);
        int transferredToEnemy = Mathf.Max(0, RuntimeState.LowerSand);
        RuntimeState.TurnState = CombatTurnState.Flipping;
        _turnProcessor.FlipHourglass(RuntimeState, transferredToEnemy);
        PublishEnemySandChanged(
            -1,
            CombatActionType.None,
            "Enemy Turn Start",
            previousEnemySand,
            RuntimeState.EnemySand,
            Mathf.Max(0, RuntimeState.EnemySand - previousEnemySand),
            0,
            false,
            "TurnStart");
        EventBus.Instance.Publish(new CombatHourglassFlippedEvent(RuntimeState.EnemySand, CreateSnapshot()));
        Debug.Log($"[Combat] HourglassFlipped | transferredToEnemy:{transferredToEnemy} EnemySand:{RuntimeState.EnemySand}");
    }

    private void PublishTurnSwapFlipOnly()
    {
        RuntimeState.TurnState = CombatTurnState.Flipping;
        EventBus.Instance.Publish(new CombatHourglassFlippedEvent(RuntimeState.EnemySand, CreateSnapshot()));
        Debug.Log($"[Combat] HourglassFlipped | turn swap only | EnemySand:{RuntimeState.EnemySand}");
    }

    private IEnumerator ResolveEnemyTurnRoutine()
    {
        RuntimeState.TurnState = CombatTurnState.EnemyResolving;
        RebuildEnemyOrder("EnemyTurnStarted");
        CombatTimelineEntrySnapshot[] orderSnapshot = BuildTimelineSnapshotArray();

        EventBus.Instance.Publish(new CombatTimelineStartedEvent(orderSnapshot, CreateSnapshot()));
        Debug.Log($"[Combat] EnemyTurnStarted | Count:{orderSnapshot.Length}");

        for (int i = 0; i < RuntimeState.EnemyOrder.Count; i++)
        {
            if (RuntimeState.IsCombatEnded)
            {
                yield break;
            }

            CombatTimelineEntryRuntime orderEntry = RuntimeState.EnemyOrder[i];
            CombatActorRuntime enemy = RuntimeState.GetActorById(orderEntry.SourceActorId);
            PublishEnemyEntryEvent(orderEntry, "EnemyOrderEntryStarted");

            if (enemy == null || enemy.IsDead)
            {
                PublishEnemyEntryResolved(orderEntry, false, "Dead");
                if (_enemyActionStepDelay > 0f) yield return new WaitForSeconds(_enemyActionStepDelay);
                continue;
            }

            if (enemy.BreakSkipCount > 0)
            {
                enemy.BreakSkipCount = Mathf.Max(0, enemy.BreakSkipCount - 1);
                enemy.SkipCurrentAction = enemy.BreakSkipCount > 0;
                enemy.GuardValue = enemy.MaxGuard;
                enemy.CurrentActionValue += enemy.BaseActionValue;

                PublishEnemyEntryResolved(orderEntry, false, "EnemyActionSkippedByBreak");
                Debug.Log($"[Combat] EnemyActionSkippedByBreak | {enemy.DisplayName}");
                if (_enemyActionStepDelay > 0f) yield return new WaitForSeconds(_enemyActionStepDelay);
                continue;
            }

            CombatIntentRuntime? intent = FindIntentForEnemy(enemy.ActorId);
            if (!intent.HasValue)
            {
                enemy.CurrentActionValue += enemy.BaseActionValue;
                PublishEnemyEntryResolved(orderEntry, true, "NoAction");
                if (_enemyActionStepDelay > 0f) yield return new WaitForSeconds(_enemyActionStepDelay);
                continue;
            }

            ResolveEnemyIntentForEntry(enemy, intent.Value, orderEntry);
            if (_enemyActionStepDelay > 0f) yield return new WaitForSeconds(_enemyActionStepDelay);
            EvaluateCombatEnd();
            if (RuntimeState.IsCombatEnded)
            {
                yield break;
            }
        }
    }

    private void ResolveEnemyIntentForEntry(CombatActorRuntime enemy, CombatIntentRuntime intent, CombatTimelineEntryRuntime orderEntry)
    {
        CombatIntentRuntime resolvedIntent = intent;
        bool usedFallback = false;
        int enemySandBefore = Mathf.Max(0, RuntimeState.EnemySand);

        if (RuntimeState.EnemySand < intent.EffectiveCost)
        {
            if (!TryBuildFallbackIntent(intent, out CombatIntentRuntime fallback) || RuntimeState.EnemySand < fallback.EffectiveCost)
            {
                enemy.CurrentActionValue += enemy.BaseActionValue;
                int required = fallback.EffectiveCost > 0 ? fallback.EffectiveCost : intent.EffectiveCost;
                PublishEnemySandChanged(
                    enemy.ActorId,
                    intent.ActionType,
                    string.IsNullOrWhiteSpace(intent.DisplayName) ? intent.ActionType.ToString() : intent.DisplayName,
                    enemySandBefore,
                    enemySandBefore,
                    0,
                    required,
                    false,
                    "NotEnoughSand");
                EventBus.Instance.Publish(new CombatIntentFailedEvent(
                    ToIntentSnapshot(intent),
                    $"NotEnoughSand(required:{required},current:{enemySandBefore})",
                    CreateSnapshot()));
                PublishEnemyEntryResolved(orderEntry, false, "EnemyIntentFailed");
                Debug.Log($"[Combat] EnemyIntentFailed | {enemy.DisplayName} {intent.ActionType} NotEnoughSand");
                return;
            }

            resolvedIntent = fallback;
            usedFallback = true;
        }

        RuntimeState.EnemySand = Mathf.Max(0, RuntimeState.EnemySand - resolvedIntent.EffectiveCost);
        PublishEnemySandChanged(
            enemy.ActorId,
            resolvedIntent.ActionType,
            string.IsNullOrWhiteSpace(resolvedIntent.DisplayName) ? resolvedIntent.ActionType.ToString() : resolvedIntent.DisplayName,
            enemySandBefore,
            RuntimeState.EnemySand,
            Mathf.Max(0, resolvedIntent.EffectiveCost),
            Mathf.Max(0, resolvedIntent.EffectiveCost),
            usedFallback,
            usedFallback ? "FallbackSpent" : "Spent");
        CombatActionResult result = ApplyIntent(resolvedIntent, enemy);
        enemy.CurrentActionValue += enemy.BaseActionValue;

        if (!result.Succeeded)
        {
            EventBus.Instance.Publish(new CombatIntentFailedEvent(ToIntentSnapshot(resolvedIntent), result.FailureReason ?? "Failed", CreateSnapshot()));
            PublishEnemyEntryResolved(orderEntry, false, "EnemyIntentFailed");
            return;
        }

        EventBus.Instance.Publish(new CombatIntentResolvedEvent(ToIntentSnapshot(resolvedIntent), resolvedIntent.EffectiveCost, CreateSnapshot()));
        PublishEnemyEntryResolved(orderEntry, true, usedFallback ? "EnemyFallbackResolved" : "EnemyIntentResolved");
        Debug.Log($"[Combat] {(usedFallback ? "EnemyFallbackResolved" : "EnemyIntentResolved")} | {enemy.DisplayName} {resolvedIntent.ActionType} spend:{resolvedIntent.EffectiveCost}");
    }

    private CombatIntentRuntime? FindIntentForEnemy(int enemyActorId)
    {
        for (int i = 0; i < RuntimeState.EnemyIntents.Count; i++)
        {
            if (RuntimeState.EnemyIntents[i].SourceActorId == enemyActorId)
            {
                return RuntimeState.EnemyIntents[i];
            }
        }

        return null;
    }

    private CombatActionResult ApplyIntent(CombatIntentRuntime intent, CombatActorRuntime source)
    {
        if (intent.TargetRule == CombatTargetType.AllEnemies || intent.TargetRule == CombatTargetType.AllAllies || intent.IsAoe)
        {
            if (intent.HealAmount > 0)
            {
                return _actionResolver.ResolveEnemyIntentHealAll(source, RuntimeState.Enemies, intent);
            }

            int total = 0;
            for (int i = 0; i < RuntimeState.Allies.Count; i++)
            {
                CombatActorRuntime target = RuntimeState.Allies[i];
                if (target == null || target.IsDead)
                {
                    continue;
                }

                int damaged = target.ApplyIncomingDamage(intent.HpDamage);
                total += damaged;
                if (damaged > 0)
                {
                    EventBus.Instance.Publish(new CombatActorDamagedEvent(target.ActorId, damaged, CreateSnapshot()));
                    if (target.IsDead)
                    {
                        HandleActorKilled(target, source);
                    }
                }
            }

            return new CombatActionResult(intent.ActionType, true, total, 0, 0, false, false);
        }

        CombatActorRuntime targetSingle = RuntimeState.GetActorById(intent.TargetActorId);
        if (targetSingle == null || targetSingle.IsDead || targetSingle.ActorType != CombatActorType.Ally)
        {
            targetSingle = RuntimeState.GetFirstAliveAlly();
        }

        CombatActionResult result = _actionResolver.ResolveEnemyIntentSingleTarget(source, targetSingle, intent);
        if (result.Succeeded && result.HpDamage > 0 && targetSingle != null)
        {
            EventBus.Instance.Publish(new CombatActorDamagedEvent(targetSingle.ActorId, result.HpDamage, CreateSnapshot()));
            if (targetSingle.IsDead)
            {
                HandleActorKilled(targetSingle, source);
            }
        }

        return result;
    }

    private bool TryBuildFallbackIntent(CombatIntentRuntime intent, out CombatIntentRuntime fallback)
    {
        fallback = default;
        if (intent.FallbackActionType == CombatActionType.None)
        {
            return false;
        }

        if (!_actionByType.TryGetValue(intent.FallbackActionType, out CombatActionDataSO fallbackAction) || fallbackAction == null)
        {
            return false;
        }

        int minCost = Mathf.Max(1, RuntimeState.EnemyCostMin);
        int effectiveCost = Mathf.Max(minCost, Mathf.Max(0, fallbackAction.baseCost) - RuntimeState.Pressure);
        fallback = intent;
        fallback.ActionType = fallbackAction.actionType;
        fallback.DisplayName = string.IsNullOrWhiteSpace(fallbackAction.displayName) ? fallbackAction.name : fallbackAction.displayName;
        fallback.BaseCost = Mathf.Max(0, fallbackAction.baseCost);
        fallback.MinEffectiveCost = minCost;
        fallback.EffectiveCost = effectiveCost;
        fallback.Speed = Mathf.Max(0, fallbackAction.speed > 0 ? fallbackAction.speed : intent.Speed);
        fallback.HpDamage = Mathf.Max(0, fallbackAction.hpDamage);
        fallback.GuardDamage = Mathf.Max(0, fallbackAction.guardDamage);
        fallback.HealAmount = Mathf.Max(0, fallbackAction.healAmount);
        fallback.TargetRule = fallbackAction.targetType;
        fallback.IsAoe = fallbackAction.isAreaAction;
        fallback.FallbackActionType = CombatActionType.None;
        fallback.TargetActorId = ResolveFallbackTarget(fallbackAction.targetType, intent.SourceActorId);
        return true;
    }

    private int ResolveFallbackTarget(CombatTargetType targetType, int sourceActorId)
    {
        if (targetType == CombatTargetType.Self)
        {
            return sourceActorId;
        }

        if (targetType == CombatTargetType.SingleAlly)
        {
            CombatActorRuntime target = RuntimeState.GetFirstAliveEnemy();
            return target != null ? target.ActorId : -1;
        }

        CombatActorRuntime ally = RuntimeState.GetFirstAliveAlly();
        return ally != null ? ally.ActorId : -1;
    }

    private void HandleEnemyBroken(CombatActorRuntime enemy)
    {
        if (enemy == null || enemy.ActorType != CombatActorType.Enemy || enemy.IsDead)
        {
            return;
        }

        enemy.BreakSkipCount += 1;
        enemy.SkipCurrentAction = true;
        enemy.CurrentActionValue += enemy.BaseActionValue * Mathf.Max(0f, enemy.BreakDelayRatio);

        EventBus.Instance.Publish(new CombatActorBrokenEvent(enemy.ActorId, CreateSnapshot()));
        RebuildEnemyOrder("ActorBroken");
        Debug.Log($"[Combat] ActorBroken | {enemy.DisplayName} SkipCount:{enemy.BreakSkipCount}");
    }

    private void HandleActorKilled(CombatActorRuntime victim, CombatActorRuntime killer)
    {
        if (victim == null)
        {
            return;
        }

        EventBus.Instance.Publish(new CombatActorKilledEvent(victim.ActorId, CreateSnapshot()));

        if (victim.ActorType == CombatActorType.Enemy)
        {
            RebuildEnemyOrder("ActorKilled");
        }

        if (killer != null
            && killer.ActorType == CombatActorType.Ally
            && victim.ActorType == CombatActorType.Enemy
            && RuntimeState.EnableKillBonus
            && RuntimeState.MaxKillBonusPerRound > 0
            && !RuntimeState.KillBonusGrantedThisRound)
        {
            RuntimeState.KillBonusGrantedThisRound = true;
            RuntimeState.KillBonusToken += 1;
            EventBus.Instance.Publish(new CombatKillBonusGrantedEvent(killer.ActorId, RuntimeState.KillBonusToken, CreateSnapshot()));
            Debug.Log($"[Combat] KillBonusGranted | Token:{RuntimeState.KillBonusToken}");
        }
    }

    private void PublishGuardChangedIfNeeded(CombatActorRuntime actor, int before, int after)
    {
        if (actor == null || before == after)
        {
            return;
        }

        EventBus.Instance.Publish(new CombatActorGuardChangedEvent(actor.ActorId, before, after, CreateSnapshot()));
    }

    private void RebuildEnemyOrder(string reason)
    {
        RuntimeState.EnemyOrder.Clear();

        List<CombatActorRuntime> aliveEnemies = new List<CombatActorRuntime>(RuntimeState.Enemies.Count);
        for (int i = 0; i < RuntimeState.Enemies.Count; i++)
        {
            CombatActorRuntime enemy = RuntimeState.Enemies[i];
            if (enemy != null && !enemy.IsDead)
            {
                aliveEnemies.Add(enemy);
            }
        }

        aliveEnemies.Sort(CompareEnemyOrder);

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            CombatActorRuntime enemy = aliveEnemies[i];
            bool isSkipped = enemy.BreakSkipCount > 0;
            bool isDelayed = enemy.CurrentActionValue > (enemy.BaseActionValue + 0.01f);
            string status = isSkipped ? "Skip" : (isDelayed ? "Delay" : "Ready");

            RuntimeState.EnemyOrder.Add(new CombatTimelineEntryRuntime
            {
                TimelineIndex = i,
                Side = CombatTimelineSide.Enemy,
                EntryType = CombatTimelineEntryType.Intent,
                SourceActorId = enemy.ActorId,
                SourceSlotIndex = enemy.SlotIndex,
                TargetActorId = -1,
                ActionType = CombatActionType.None,
                Cost = 0,
                Speed = 0,
                DisplayName = enemy.DisplayName,
                Status = status,
                IsCurrent = false,
                IsSkipped = isSkipped,
                IsDelayed = isDelayed,
                IsDead = false
            });
        }

        CombatTimelineEntrySnapshot[] snapshots = BuildTimelineSnapshotArray();
        EventBus.Instance.Publish(new CombatEnemyOrderChangedEvent(snapshots, CreateSnapshot()));
        EventBus.Instance.Publish(new CombatTimelinePreviewChangedEvent(snapshots, CreateSnapshot()));
        Debug.Log($"[Combat] EnemyOrderChanged | {reason} | Count:{snapshots.Length}");
    }

    private static int CompareEnemyOrder(CombatActorRuntime a, CombatActorRuntime b)
    {
        int compareValue = a.CurrentActionValue.CompareTo(b.CurrentActionValue);
        if (compareValue != 0)
        {
            return compareValue;
        }

        return a.SlotIndex.CompareTo(b.SlotIndex);
    }

    private void PublishEnemyEntryEvent(CombatTimelineEntryRuntime entry, string message)
    {
        CombatTimelineEntrySnapshot snapshot = ToTimelineSnapshot(entry, message);
        EventBus.Instance.Publish(new CombatEnemyOrderEntryResolvedEvent(snapshot, message, CreateSnapshot()));
    }

    private void PublishEnemyEntryResolved(CombatTimelineEntryRuntime entry, bool succeeded, string message)
    {
        CombatTimelineEntrySnapshot snapshot = ToTimelineSnapshot(entry, message);
        EventBus.Instance.Publish(new CombatEnemyOrderEntryResolvedEvent(snapshot, message, CreateSnapshot()));
        EventBus.Instance.Publish(new CombatTimelineEntryResolvedEvent(snapshot, succeeded, message, CreateSnapshot()));
    }

    private void EvaluateCombatEnd()
    {
        bool enemyDead = RuntimeState.CountAliveEnemies() <= 0;
        bool allyDead = RuntimeState.CountAliveAllies() <= 0;
        if (!enemyDead && !allyDead)
        {
            return;
        }

        RuntimeState.IsCombatEnded = true;
        RuntimeState.TurnState = CombatTurnState.Ended;
        EventBus.Instance.Publish(new CombatEndedEvent(enemyDead && !allyDead, CreateSnapshot()));
        Debug.Log($"[Combat] CombatEnded | PlayerWon:{enemyDead && !allyDead}");
    }

    private bool RequiresEnemyTarget(CombatActionDataSO actionData)
    {
        return actionData != null && actionData.targetType == CombatTargetType.SingleEnemy && !actionData.isAreaAction;
    }

    private int FindFirstQueuedCommandIndex(int actorId)
    {
        for (int i = 0; i < RuntimeState.QueuedCommands.Count; i++)
        {
            if (RuntimeState.QueuedCommands[i].SourceActorId == actorId)
            {
                return i;
            }
        }

        return -1;
    }

    private int CountQueuedCommandsForActor(int actorId)
    {
        int count = 0;
        for (int i = 0; i < RuntimeState.QueuedCommands.Count; i++)
        {
            if (RuntimeState.QueuedCommands[i].SourceActorId == actorId)
            {
                count += 1;
            }
        }

        return count;
    }

    private int GetFirstQueuedCostForActor(int actorId)
    {
        for (int i = 0; i < RuntimeState.QueuedCommands.Count; i++)
        {
            if (RuntimeState.QueuedCommands[i].SourceActorId == actorId)
            {
                return RuntimeState.QueuedCommands[i].Cost;
            }
        }

        return 0;
    }

    private int FindNextAliveEnemySlot(int from)
    {
        int count = RuntimeState.Enemies.Count;
        for (int i = 1; i <= count; i++)
        {
            int slot = (from + i) % count;
            CombatActorRuntime actor = RuntimeState.Enemies[slot];
            if (actor != null && !actor.IsDead)
            {
                return slot;
            }
        }

        return Mathf.Clamp(from, 0, Mathf.Max(0, count - 1));
    }

    private bool TryAutoSelectNextCommandableAlly(int fromSlot)
    {
        if (RuntimeState == null || RuntimeState.Allies.Count == 0)
        {
            return false;
        }

        int count = RuntimeState.Allies.Count;
        int safeFrom = Mathf.Clamp(fromSlot, 0, count - 1);
        for (int i = 1; i <= count; i++)
        {
            int slot = (safeFrom + i) % count;
            CombatActorRuntime actor = RuntimeState.Allies[slot];
            if (!CanActorTakeAnyAction(actor))
            {
                continue;
            }

            RuntimeState.SelectedAllySlot = slot;
            PublishActorSelected(CombatActorType.Ally, slot);
            return true;
        }

        return false;
    }

    private bool CanActorTakeAnyAction(CombatActorRuntime actor)
    {
        if (RuntimeState == null || actor == null || actor.IsDead || actor.HasActedThisRound)
        {
            return false;
        }

        int remainingUpper = Mathf.Max(0, RuntimeState.UpperSand);
        if (actor.ActionList == null || actor.ActionList.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < actor.ActionList.Count; i++)
        {
            CombatActionDataSO action = actor.ActionList[i];
            if (action == null)
            {
                continue;
            }

            if (remainingUpper < Mathf.Max(0, action.baseCost))
            {
                continue;
            }

            if (RequiresEnemyTarget(action) && RuntimeState.GetFirstAliveEnemy() == null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void EnsureSelectionsAreAlive()
    {
        if (RuntimeState.Allies.Count > 0)
        {
            int slot = RuntimeState.SelectedAllySlot;
            if (slot < 0 || slot >= RuntimeState.Allies.Count || RuntimeState.Allies[slot] == null || RuntimeState.Allies[slot].IsDead)
            {
                RuntimeState.SelectedAllySlot = FindFirstAliveSlot(RuntimeState.Allies);
            }
        }

        if (RuntimeState.Enemies.Count > 0)
        {
            int slot = RuntimeState.SelectedEnemySlot;
            if (slot < 0 || slot >= RuntimeState.Enemies.Count || RuntimeState.Enemies[slot] == null || RuntimeState.Enemies[slot].IsDead)
            {
                RuntimeState.SelectedEnemySlot = FindFirstAliveSlot(RuntimeState.Enemies);
            }
        }
    }

    private static int FindFirstAliveSlot(List<CombatActorRuntime> actors)
    {
        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i] != null && !actors[i].IsDead)
            {
                return i;
            }
        }

        return 0;
    }

    private void PublishActorSelected(CombatActorType teamType, int slotIndex)
    {
        int actorId = -1;
        if (teamType == CombatActorType.Ally && slotIndex >= 0 && slotIndex < RuntimeState.Allies.Count)
        {
            actorId = RuntimeState.Allies[slotIndex] != null ? RuntimeState.Allies[slotIndex].ActorId : -1;
        }
        else if (teamType == CombatActorType.Enemy && slotIndex >= 0 && slotIndex < RuntimeState.Enemies.Count)
        {
            actorId = RuntimeState.Enemies[slotIndex] != null ? RuntimeState.Enemies[slotIndex].ActorId : -1;
        }

        EventBus.Instance.Publish(new CombatActorSelectedEvent(teamType, slotIndex, actorId, CreateSnapshot()));
    }

    private void PublishEnemySandChanged(
        int actorId,
        CombatActionType actionType,
        string intentName,
        int beforeEnemySand,
        int afterEnemySand,
        int spentEnemySand,
        int requiredEnemySand,
        bool usedFallback,
        string reason)
    {
        EventBus.Instance.Publish(new CombatEnemySandChangedEvent(
            actorId,
            actionType,
            intentName ?? string.Empty,
            Mathf.Max(0, beforeEnemySand),
            Mathf.Max(0, afterEnemySand),
            Mathf.Max(0, spentEnemySand),
            Mathf.Max(0, requiredEnemySand),
            usedFallback,
            reason ?? string.Empty,
            CreateSnapshot()));
    }

    private CombatLogSnapshot CreateSnapshot()
    {
        return new CombatLogSnapshot(
            RuntimeState != null ? RuntimeState.TurnIndex : 0,
            RuntimeState != null ? RuntimeState.TurnState : CombatTurnState.None,
            RuntimeState != null ? RuntimeState.UpperSand : 0,
            RuntimeState != null ? RuntimeState.LowerSand : 0,
            RuntimeState != null ? RuntimeState.PlayerSpend : 0,
            RuntimeState != null ? RuntimeState.EnemySand : 0,
            RuntimeState != null ? RuntimeState.MinimumFall : 0,
            RuntimeState != null ? RuntimeState.Pressure : 0,
            RuntimeState != null ? RuntimeState.KillBonusToken : 0,
            RuntimeState != null ? RuntimeState.SelectedAllySlot : 0,
            RuntimeState != null ? RuntimeState.SelectedEnemySlot : 0,
            RuntimeState != null ? BuildActorSnapshots(RuntimeState.Allies) : Array.Empty<CombatActorSnapshot>(),
            RuntimeState != null ? BuildActorSnapshots(RuntimeState.Enemies) : Array.Empty<CombatActorSnapshot>(),
            RuntimeState != null ? BuildIntentSnapshots(RuntimeState.EnemyIntents) : Array.Empty<CombatIntentSnapshot>(),
            RuntimeState != null ? BuildTimelineSnapshotArray() : Array.Empty<CombatTimelineEntrySnapshot>());
    }

    private static CombatActorSnapshot[] BuildActorSnapshots(List<CombatActorRuntime> actors)
    {
        CombatActorSnapshot[] snapshots = new CombatActorSnapshot[actors.Count];
        for (int i = 0; i < actors.Count; i++)
        {
            CombatActorRuntime actor = actors[i];
            if (actor == null)
            {
                snapshots[i] = new CombatActorSnapshot(-1, "None", CombatActorType.None, i, 0, 0, 0, 0, true, false);
                continue;
            }

            snapshots[i] = new CombatActorSnapshot(
                actor.ActorId,
                actor.DisplayName,
                actor.ActorType,
                actor.SlotIndex,
                actor.CurrentHp,
                actor.MaxHp,
                actor.GuardValue,
                actor.MaxGuard,
                actor.IsDead,
                actor.BreakSkipCount > 0);
        }

        return snapshots;
    }

    private static CombatIntentSnapshot[] BuildIntentSnapshots(List<CombatIntentRuntime> intents)
    {
        CombatIntentSnapshot[] snapshots = new CombatIntentSnapshot[intents.Count];
        for (int i = 0; i < intents.Count; i++)
        {
            snapshots[i] = ToIntentSnapshot(intents[i]);
        }

        return snapshots;
    }

    private CombatTimelineEntrySnapshot[] BuildTimelineSnapshotArray()
    {
        CombatTimelineEntrySnapshot[] snapshots = new CombatTimelineEntrySnapshot[RuntimeState.EnemyOrder.Count];
        for (int i = 0; i < RuntimeState.EnemyOrder.Count; i++)
        {
            snapshots[i] = ToTimelineSnapshot(RuntimeState.EnemyOrder[i], RuntimeState.EnemyOrder[i].Status);
        }

        return snapshots;
    }

    private static CombatIntentSnapshot ToIntentSnapshot(CombatIntentRuntime intent)
    {
        return new CombatIntentSnapshot(
            intent.SourceActorId,
            intent.SourceSlotIndex,
            intent.ActionType,
            intent.TargetActorId,
            intent.BaseCost,
            intent.EffectiveCost,
            intent.Speed,
            intent.FallbackActionType);
    }

    private static CombatTimelineEntrySnapshot ToTimelineSnapshot(CombatTimelineEntryRuntime entry, string status)
    {
        return new CombatTimelineEntrySnapshot(
            entry.TimelineIndex,
            CombatTimelineSide.Enemy,
            CombatTimelineEntryType.Intent,
            entry.SourceActorId,
            entry.SourceSlotIndex,
            entry.TargetActorId,
            entry.ActionType,
            0,
            0,
            entry.DisplayName,
            status ?? entry.Status);
    }

    private void DebugTeamStatus(string tag)
    {
        Debug.Log($"[Combat] {tag} | Allies:{BuildTeamStatus(RuntimeState.Allies)} | Enemies:{BuildTeamStatus(RuntimeState.Enemies)} | Upper:{RuntimeState.UpperSand} Lower:{RuntimeState.LowerSand} EnemySand:{RuntimeState.EnemySand}");
    }

    private static string BuildTeamStatus(List<CombatActorRuntime> team)
    {
        string value = string.Empty;
        for (int i = 0; i < team.Count; i++)
        {
            CombatActorRuntime actor = team[i];
            if (actor == null)
            {
                value += "(null)";
            }
            else
            {
                value += $"{actor.DisplayName}(HP:{actor.CurrentHp}/{actor.MaxHp},G:{actor.GuardValue}/{actor.MaxGuard},Skip:{actor.BreakSkipCount},Dead:{actor.IsDead})";
            }

            if (i < team.Count - 1)
            {
                value += ", ";
            }
        }

        return value;
    }

    private CombatActorDataSO ToActorData(int actorId, CombatActorType teamType)
    {
        CombatActorDataSO[] pool = teamType == CombatActorType.Ally ? _config.allyPartyActors : _config.enemyPartyActors;
        if (pool == null)
        {
            return null;
        }

        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null && pool[i].actorId == actorId)
            {
                return pool[i];
            }
        }

        return null;
    }
}
