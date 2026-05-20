using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-40)]
public class CombatScreenPresenter : MonoBehaviour
{
    [Header("Main Views")]
    [SerializeField] private CombatActorPanelView playerStatusView;
    [SerializeField] private CombatActorPanelView enemyStatusView;
    [SerializeField] private CombatActionPanelView actionPanelView;
    [SerializeField] private HourglassSandView hourglassView;
    [SerializeField] private CombatBoardView boardView;
    [SerializeField] private CombatTimelineView timelineView;
    [SerializeField] private CombatFeedbackView combatFeedbackView;
    [SerializeField] private CombatLogView combatLogView;

    [Header("Optional Debug")]
    [SerializeField] private TMP_Text alliesSummaryText;
    [SerializeField] private TMP_Text enemiesSummaryText;
    [SerializeField] private TMP_Text intentsSummaryText;
    [SerializeField] private TMP_Text pressureSummaryText;

    private HourglassCombatManager _combatManager;
    private CombatTimelineEntrySnapshot[] _lastTimelinePreview = System.Array.Empty<CombatTimelineEntrySnapshot>();
    private CombatActionDataSO _basicAction;
    private CombatActionDataSO _guardAction;
    private CombatActionDataSO _specialAction;
    private CombatActionDataSO _pendingEnemyTargetAction;
    private int _pendingSourceActorId = -1;

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Subscribe<CombatIntentShownEvent>(OnCombatIntentShown);
        EventBus.Instance.Subscribe<CombatCommandQueuedEvent>(OnCombatCommandQueued);
        EventBus.Instance.Subscribe<CombatCommandConfirmedEvent>(OnCombatCommandConfirmed);
        EventBus.Instance.Subscribe<CombatAllyCommandResolvedEvent>(OnCombatAllyCommandResolved);
        EventBus.Instance.Subscribe<CombatMinimumFallAppliedEvent>(OnCombatMinimumFallApplied);
        EventBus.Instance.Subscribe<CombatHourglassFlippedEvent>(OnCombatHourglassFlipped);
        EventBus.Instance.Subscribe<CombatEnemyOrderChangedEvent>(OnCombatEnemyOrderChanged);
        EventBus.Instance.Subscribe<CombatEnemyOrderEntryResolvedEvent>(OnCombatEnemyOrderEntryResolved);
        EventBus.Instance.Subscribe<CombatTimelinePreviewChangedEvent>(OnCombatTimelinePreviewChanged);
        EventBus.Instance.Subscribe<CombatTimelineStartedEvent>(OnCombatTimelineStarted);
        EventBus.Instance.Subscribe<CombatTimelineEntryResolvedEvent>(OnCombatTimelineEntryResolved);
        EventBus.Instance.Subscribe<CombatIntentResolvedEvent>(OnCombatIntentResolved);
        EventBus.Instance.Subscribe<CombatIntentFailedEvent>(OnCombatIntentFailed);
        EventBus.Instance.Subscribe<CombatActorSelectedEvent>(OnCombatActorSelected);
        EventBus.Instance.Subscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Subscribe<CombatActorGuardChangedEvent>(OnCombatActorGuardChanged);
        EventBus.Instance.Subscribe<CombatActorBrokenEvent>(OnCombatActorBroken);
        EventBus.Instance.Subscribe<CombatActorKilledEvent>(OnCombatActorKilled);
        EventBus.Instance.Subscribe<CombatKillBonusGrantedEvent>(OnCombatKillBonusGranted);
        EventBus.Instance.Subscribe<CombatPressureChangedEvent>(OnCombatPressureChanged);
        EventBus.Instance.Subscribe<CombatEndedEvent>(OnCombatEnded);

        EventBus.Instance.Subscribe<CombatStrikeInputEvent>(OnCombatBasicInput);
        EventBus.Instance.Subscribe<CombatPierceInputEvent>(OnCombatSpecialInput);
        EventBus.Instance.Subscribe<CombatHexInputEvent>(OnCombatGuardSkillInput);
        EventBus.Instance.Subscribe<CombatGuardInputEvent>(OnCombatGuardInput);
    }

    private void Start()
    {
        CacheCombatManager();
        if (_combatManager != null)
        {
            hourglassView?.SetFlipDuration(_combatManager.FlipDuration);
            boardView?.Bind(_combatManager);
        }

        combatFeedbackView?.Initialize();
        actionPanelView?.SetStaticTexts();
        BindButtons();
        RefreshViews();
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<CombatRoundStartedEvent>(OnCombatRoundStarted);
        EventBus.Instance.Unsubscribe<CombatIntentShownEvent>(OnCombatIntentShown);
        EventBus.Instance.Unsubscribe<CombatCommandQueuedEvent>(OnCombatCommandQueued);
        EventBus.Instance.Unsubscribe<CombatCommandConfirmedEvent>(OnCombatCommandConfirmed);
        EventBus.Instance.Unsubscribe<CombatAllyCommandResolvedEvent>(OnCombatAllyCommandResolved);
        EventBus.Instance.Unsubscribe<CombatMinimumFallAppliedEvent>(OnCombatMinimumFallApplied);
        EventBus.Instance.Unsubscribe<CombatHourglassFlippedEvent>(OnCombatHourglassFlipped);
        EventBus.Instance.Unsubscribe<CombatEnemyOrderChangedEvent>(OnCombatEnemyOrderChanged);
        EventBus.Instance.Unsubscribe<CombatEnemyOrderEntryResolvedEvent>(OnCombatEnemyOrderEntryResolved);
        EventBus.Instance.Unsubscribe<CombatTimelinePreviewChangedEvent>(OnCombatTimelinePreviewChanged);
        EventBus.Instance.Unsubscribe<CombatTimelineStartedEvent>(OnCombatTimelineStarted);
        EventBus.Instance.Unsubscribe<CombatTimelineEntryResolvedEvent>(OnCombatTimelineEntryResolved);
        EventBus.Instance.Unsubscribe<CombatIntentResolvedEvent>(OnCombatIntentResolved);
        EventBus.Instance.Unsubscribe<CombatIntentFailedEvent>(OnCombatIntentFailed);
        EventBus.Instance.Unsubscribe<CombatActorSelectedEvent>(OnCombatActorSelected);
        EventBus.Instance.Unsubscribe<CombatActorDamagedEvent>(OnCombatActorDamaged);
        EventBus.Instance.Unsubscribe<CombatActorGuardChangedEvent>(OnCombatActorGuardChanged);
        EventBus.Instance.Unsubscribe<CombatActorBrokenEvent>(OnCombatActorBroken);
        EventBus.Instance.Unsubscribe<CombatActorKilledEvent>(OnCombatActorKilled);
        EventBus.Instance.Unsubscribe<CombatKillBonusGrantedEvent>(OnCombatKillBonusGranted);
        EventBus.Instance.Unsubscribe<CombatPressureChangedEvent>(OnCombatPressureChanged);
        EventBus.Instance.Unsubscribe<CombatEndedEvent>(OnCombatEnded);

        EventBus.Instance.Unsubscribe<CombatStrikeInputEvent>(OnCombatBasicInput);
        EventBus.Instance.Unsubscribe<CombatPierceInputEvent>(OnCombatSpecialInput);
        EventBus.Instance.Unsubscribe<CombatHexInputEvent>(OnCombatGuardSkillInput);
        EventBus.Instance.Unsubscribe<CombatGuardInputEvent>(OnCombatGuardInput);
    }

    private void CacheCombatManager()
    {
        if (_combatManager == null)
        {
            _combatManager = HourglassCombatManager.Instance;
        }
    }

    private void BindButtons()
    {
        if (actionPanelView == null)
        {
            return;
        }

        BindButton(actionPanelView.BasicButton, RequestBasicAction);
        BindButton(actionPanelView.SpecialButton, RequestSpecialAction);
        BindButton(actionPanelView.GuardButton, RequestGuardAction);
        BindButton(actionPanelView.FlipButton, RequestFlip);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void RefreshViews(bool refreshActionPanel = true)
    {
        CacheCombatManager();
        CombatRuntimeState state = _combatManager != null ? _combatManager.RuntimeState : null;
        if (state == null)
        {
            SetAllButtonsInteractable(false);
            return;
        }

        CombatActorRuntime selectedAlly = state.GetSelectedAlly();
        CombatActorRuntime selectedEnemy = state.GetSelectedEnemy();
        bool planning = state.TurnState == CombatTurnState.PlayerCommand && !state.IsCombatEnded;
        int pressureMax = state.DifficultyData != null ? Mathf.Max(1, state.DifficultyData.pressureMax) : 2;

        playerStatusView?.ApplyActorState(selectedAlly, planning, true);
        enemyStatusView?.ApplyActorState(selectedEnemy, planning, false);
        playerStatusView?.SetIntentText(string.Empty);
        enemyStatusView?.SetIntentText(BuildSelectedEnemyIntentText(state, selectedEnemy));
        playerStatusView?.SetPressureState(false, 0, pressureMax);
        enemyStatusView?.SetPressureState(true, state.Pressure, pressureMax);
        hourglassView?.Refresh(state);
        boardView?.Refresh(state);
        timelineView?.SetTimeline(_lastTimelinePreview);

        if (refreshActionPanel)
        {
            RefreshActionPanelForSelectedAlly(state);
        }
        else
        {
            UpdateActionPanelInteractivity(state);
        }
        UpdateDebugText(state);
    }

    private void RefreshActionPanelForSelectedAlly(CombatRuntimeState state)
    {
        if (actionPanelView == null || _combatManager == null || state == null)
        {
            return;
        }

        bool planning = state.TurnState == CombatTurnState.PlayerCommand && !state.IsCombatEnded;
        CombatActorRuntime selectedAlly = state.GetSelectedAlly();
        ResolveSelectedAllyActions(selectedAlly, out _basicAction, out _guardAction, out _specialAction);

        ApplyActionSlot(0, "Q Basic", _basicAction, planning);
        ApplyActionSlot(1, "W Special", _specialAction, planning);
        ApplyActionSlot(2, "E Guard", _guardAction, planning);
        int predictedEnemySand = Mathf.Max(state.MinimumFall, state.LowerSand);
        actionPanelView.SetEndTurnPreview(true, predictedEnemySand);
        actionPanelView.SetFlipInteractable(planning);
    }

    private void UpdateActionPanelInteractivity(CombatRuntimeState state)
    {
        if (actionPanelView == null || _combatManager == null || state == null)
        {
            return;
        }

        bool planning = state.TurnState == CombatTurnState.PlayerCommand && !state.IsCombatEnded;
        ApplyActionSlot(0, "Q Basic", _basicAction, planning);
        ApplyActionSlot(1, "W Special", _specialAction, planning);
        ApplyActionSlot(2, "E Guard", _guardAction, planning);
        int predictedEnemySand = Mathf.Max(state.MinimumFall, state.LowerSand);
        actionPanelView.SetEndTurnPreview(true, predictedEnemySand);
        actionPanelView.SetFlipInteractable(planning);
    }

    private void ApplyActionSlot(int slotIndex, string emptyName, CombatActionDataSO action, bool planning)
    {
        string name = emptyName;
        int cost = 0;
        string effect = "-";
        bool interactable = false;

        if (action != null)
        {
            name = string.IsNullOrWhiteSpace(action.displayName) ? action.name : action.displayName;
            cost = action.baseCost;
            effect = BuildActionEffectText(action);
            interactable = planning && _combatManager.CanQueueActionForSelectedAlly(action, out _);
        }

        actionPanelView.SetActionSlot(slotIndex, name, cost, effect, interactable, true);
    }

    private static void ResolveSelectedAllyActions(
        CombatActorRuntime selectedAlly,
        out CombatActionDataSO basicAction,
        out CombatActionDataSO guardAction,
        out CombatActionDataSO specialAction)
    {
        basicAction = null;
        guardAction = null;
        specialAction = null;
        if (selectedAlly == null || selectedAlly.ActionList == null || selectedAlly.ActionList.Count == 0)
        {
            return;
        }

        for (int i = 0; i < selectedAlly.ActionList.Count; i++)
        {
            CombatActionDataSO action = selectedAlly.ActionList[i];
            if (action == null)
            {
                continue;
            }

            if (basicAction == null && IsBasicAttackAction(action))
            {
                basicAction = action;
                continue;
            }

            if (guardAction == null && action.actionType == CombatActionType.Guard)
            {
                guardAction = action;
                continue;
            }
        }

        if (basicAction == null)
        {
            basicAction = selectedAlly.ActionList.Find(IsBasicAttackAction);
        }

        for (int i = 0; i < selectedAlly.ActionList.Count; i++)
        {
            CombatActionDataSO action = selectedAlly.ActionList[i];
            if (action == null || action == basicAction || action == guardAction)
            {
                continue;
            }

            specialAction = action;
            break;
        }

        if (basicAction == null)
        {
            Debug.LogWarning($"[Combat] Basic attack action missing for ally '{selectedAlly.DisplayName}'.");
        }

        if (guardAction == null)
        {
            Debug.LogWarning($"[Combat] Guard action missing for ally '{selectedAlly.DisplayName}'.");
        }

        if (specialAction == null)
        {
            Debug.LogWarning($"[Combat] Special action missing for ally '{selectedAlly.DisplayName}'.");
        }
    }

    private static bool IsBasicAttackAction(CombatActionDataSO action)
    {
        if (action == null)
        {
            return false;
        }

        if (action.actionType == CombatActionType.BasicAttack || action.actionType == CombatActionType.Strike)
        {
            return true;
        }

        string id = string.IsNullOrWhiteSpace(action.actionId) ? action.name : action.actionId;
        string lower = id.ToLowerInvariant();
        return lower.Contains("basic") || lower.Contains("attack") || lower.Contains("strike");
    }

    private static string BuildActionEffectText(CombatActionDataSO action)
    {
        if (action == null)
        {
            return "-";
        }

        StringBuilder builder = new StringBuilder();
        if (action.hpDamage > 0) builder.Append($"HP-{action.hpDamage} ");
        if (action.guardDamage > 0) builder.Append($"G-{action.guardDamage} ");
        if (action.guardGain > 0) builder.Append($"G+{action.guardGain} ");
        if (action.healAmount > 0) builder.Append($"Heal+{action.healAmount} ");
        if (builder.Length == 0) builder.Append("-");
        return builder.ToString().Trim();
    }

    private void UpdateDebugText(CombatRuntimeState state)
    {
        if (alliesSummaryText != null)
        {
            alliesSummaryText.text = BuildTeamSummary("Allies", state.Allies, state.SelectedAllySlot);
        }

        if (enemiesSummaryText != null)
        {
            enemiesSummaryText.text = BuildTeamSummary("Enemies", state.Enemies, state.SelectedEnemySlot);
        }

        if (intentsSummaryText != null)
        {
            intentsSummaryText.text = BuildIntentSummary(state.EnemyIntents);
        }

        if (pressureSummaryText != null)
        {
            pressureSummaryText.text = $"Pressure:{state.Pressure} Token:{state.KillBonusToken} Upper:{state.UpperSand} Lower:{state.LowerSand} MinFall:{state.MinimumFall} EnemySand:{state.EnemySand} Spend:{state.PlayerSpend}";
        }
    }

    private static string BuildTeamSummary(string label, System.Collections.Generic.List<CombatActorRuntime> team, int selectedSlot)
    {
        if (team == null || team.Count == 0)
        {
            return label + ": -";
        }

        StringBuilder builder = new StringBuilder(label + ": ");
        for (int i = 0; i < team.Count; i++)
        {
            CombatActorRuntime actor = team[i];
            if (actor == null)
            {
                builder.Append($"[{i}]null");
            }
            else
            {
                string selected = i == selectedSlot ? "*" : "";
                builder.Append($"[{i}]{selected}{actor.DisplayName} HP{actor.CurrentHp}/{actor.MaxHp} G{actor.GuardValue}/{actor.MaxGuard}");
            }

            if (i < team.Count - 1)
            {
                builder.Append(" | ");
            }
        }

        return builder.ToString();
    }

    private static string BuildIntentSummary(System.Collections.Generic.List<CombatIntentRuntime> intents)
    {
        if (intents == null || intents.Count == 0)
        {
            return "Intent: -";
        }

        StringBuilder builder = new StringBuilder("Intent: ");
        for (int i = 0; i < intents.Count; i++)
        {
            CombatIntentRuntime intent = intents[i];
            builder.Append($"[{intent.SourceSlotIndex}] {intent.DisplayName} c{intent.EffectiveCost}/{intent.BaseCost} s{intent.Speed}");
            if (i < intents.Count - 1)
            {
                builder.Append(" | ");
            }
        }

        return builder.ToString();
    }

    private static string BuildSelectedEnemyIntentText(CombatRuntimeState state, CombatActorRuntime selectedEnemy)
    {
        if (state == null || selectedEnemy == null || selectedEnemy.IsDead || selectedEnemy.ActorType != CombatActorType.Enemy)
        {
            return "-";
        }

        CombatIntentRuntime? selectedIntent = FindIntent(state.EnemyIntents, selectedEnemy.ActorId);
        if (!selectedIntent.HasValue)
        {
            return selectedEnemy.BreakSkipCount > 0 ? "대기" : "-";
        }

        CombatIntentRuntime intent = selectedIntent.Value;
        if (string.IsNullOrWhiteSpace(intent.DisplayName))
        {
            return "-";
        }

        return $"{intent.DisplayName} / Cost {intent.EffectiveCost}";
    }

    private static CombatIntentRuntime? FindIntent(System.Collections.Generic.List<CombatIntentRuntime> intents, int actorId)
    {
        if (intents == null)
        {
            return null;
        }

        for (int i = 0; i < intents.Count; i++)
        {
            if (intents[i].SourceActorId == actorId)
            {
                return intents[i];
            }
        }

        return null;
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        actionPanelView?.SetAllInteractable(interactable, interactable, interactable, interactable);
    }

    private void OnCombatRoundStarted(CombatRoundStartedEvent evt)
    {
        ClearPendingTargeting();
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Round Start");
        RefreshViews();
    }

    private void OnCombatIntentShown(CombatIntentShownEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Intent {evt.Intent.action_type} c{evt.Intent.effective_cost}/{evt.Intent.base_cost} s{evt.Intent.speed}");
        RefreshViews();
    }

    private void OnCombatCommandQueued(CombatCommandQueuedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Queue {evt.Command.DisplayName} c{evt.Command.Cost} s{evt.Command.Speed} -> U:{evt.PredictedUpperSand} L:{evt.PredictedLowerSand}");
        hourglassView?.AnimateQueuedSpend(evt.PredictedUpperSand, evt.PredictedLowerSand);
        RefreshViews();
    }

    private void OnCombatCommandConfirmed(CombatCommandConfirmedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Confirm spend {evt.PlayerSpend}");
        RefreshViews();
    }

    private void OnCombatAllyCommandResolved(CombatAllyCommandResolvedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] AllyCommandResolved {evt.Command.ActionType} {(evt.Succeeded ? "OK" : "FAIL")} ({evt.Message})");
        RefreshViews();
    }

    private void OnCombatMinimumFallApplied(CombatMinimumFallAppliedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] MinFall forced {evt.ForcedAmount}");
        hourglassView?.AnimateMinimumFall(evt.UpperAfter, evt.LowerAfter, evt.ForcedAmount);
        RefreshViews();
    }

    private void OnCombatHourglassFlipped(CombatHourglassFlippedEvent evt)
    {
        CacheCombatManager();
        hourglassView?.QueueFlipPreview(evt.Snapshot, _combatManager != null ? _combatManager.RuntimeState : null);
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Flip EnemySand={evt.EnemySand}");
        RefreshViews();
    }

    private void OnCombatEnemyOrderChanged(CombatEnemyOrderChangedEvent evt)
    {
        _lastTimelinePreview = evt.EnemyOrder ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        timelineView?.SetTimeline(_lastTimelinePreview);
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] EnemyOrderChanged ({_lastTimelinePreview.Length})");
        RefreshViews();
    }

    private void OnCombatEnemyOrderEntryResolved(CombatEnemyOrderEntryResolvedEvent evt)
    {
        if (_lastTimelinePreview != null
            && evt.Entry.timeline_index >= 0
            && evt.Entry.timeline_index < _lastTimelinePreview.Length)
        {
            CombatTimelineEntrySnapshot current = _lastTimelinePreview[evt.Entry.timeline_index];
            string status = current.status;
            if (evt.Message == "EnemyOrderEntryStarted")
            {
                status = "EnemyOrderEntryStarted";
            }
            else if (evt.Message == "EnemyActionSkippedByBreak")
            {
                status = "Skip";
            }
            else if (evt.Message == "Dead")
            {
                status = "Dead";
            }
            else if (status == "EnemyOrderEntryStarted")
            {
                status = "Ready";
            }

            _lastTimelinePreview[evt.Entry.timeline_index] = new CombatTimelineEntrySnapshot(
                current.timeline_index,
                current.side,
                current.entry_type,
                current.source_actor_id,
                current.source_slot_index,
                current.target_actor_id,
                current.action_type,
                current.cost,
                current.speed,
                current.label,
                status);
            timelineView?.SetTimeline(_lastTimelinePreview);
        }

        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] {evt.Message} {evt.Entry.label}");
        RefreshViews();
    }

    private void OnCombatTimelinePreviewChanged(CombatTimelinePreviewChangedEvent evt)
    {
        _lastTimelinePreview = evt.Timeline ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        timelineView?.SetTimeline(_lastTimelinePreview);
        RefreshViews();
    }

    private void OnCombatTimelineStarted(CombatTimelineStartedEvent evt)
    {
        _lastTimelinePreview = evt.Timeline ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        timelineView?.SetTimeline(_lastTimelinePreview);
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] EnemyTurnStarted ({_lastTimelinePreview.Length})");
        RefreshViews();
    }

    private void OnCombatTimelineEntryResolved(CombatTimelineEntryResolvedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] EnemyOrderEntryResolved {evt.Entry.label} {(evt.Succeeded ? "OK" : "FAIL")} ({evt.Message})");
        RefreshViews();
    }

    private void OnCombatIntentResolved(CombatIntentResolvedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Intent Resolved {evt.Intent.action_type} spend {evt.SpentEnemySand}");
        RefreshViews();
    }

    private void OnCombatIntentFailed(CombatIntentFailedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Intent Failed {evt.Intent.action_type} ({evt.Reason})");
        RefreshViews();
    }

    private void OnCombatActorSelected(CombatActorSelectedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Selected {evt.TeamType} slot {evt.SlotIndex}");
        if (evt.TeamType == CombatActorType.Ally)
        {
            ClearPendingTargeting();
            RefreshViews(true);
        }
        else
        {
            if (TryResolvePendingEnemyTargetAction(evt.ActorId))
            {
                return;
            }

            RefreshViews(false);
        }
    }

    private void OnCombatActorDamaged(CombatActorDamagedEvent evt)
    {
        CacheCombatManager();
        CombatActorRuntime target = _combatManager != null && _combatManager.RuntimeState != null
            ? _combatManager.RuntimeState.GetActorById(evt.ActorId)
            : null;
        bool isAlly = target != null && target.ActorType == CombatActorType.Ally;
        CombatActorPanelView panel = isAlly ? playerStatusView : enemyStatusView;
        panel?.PlayHitReaction();
        combatFeedbackView?.SpawnDamagePopup(panel != null ? panel.PopupAnchor : null, evt.Damage, isAlly ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(1f, 0.65f, 0.3f, 1f));
        combatFeedbackView?.PlayScreenPulse();
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Damage {evt.ActorId} -{evt.Damage}");
        RefreshViews();
    }

    private void OnCombatActorGuardChanged(CombatActorGuardChangedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Guard {evt.ActorId} {evt.BeforeGuard}->{evt.AfterGuard}");
        RefreshViews();
    }

    private void OnCombatActorBroken(CombatActorBrokenEvent evt)
    {
        combatFeedbackView?.ShowBreakText(enemyStatusView != null ? enemyStatusView.PopupAnchor : null);
        combatFeedbackView?.PlayScreenPulse();
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Broken {evt.ActorId}");
        RefreshViews();
    }

    private void OnCombatActorKilled(CombatActorKilledEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Killed {evt.ActorId}");
        RefreshViews();
    }

    private void OnCombatKillBonusGranted(CombatKillBonusGrantedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] KillBonus token {evt.KillBonusToken}");
        RefreshViews();
    }

    private void OnCombatPressureChanged(CombatPressureChangedEvent evt)
    {
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] Pressure {evt.PreviousPressure}->{evt.NewPressure}");
        RefreshViews();
    }

    private void OnCombatEnded(CombatEndedEvent evt)
    {
        ClearPendingTargeting();
        combatLogView?.AddLog($"[R{evt.Snapshot.round_index}] {(evt.PlayerWon ? "Victory" : "Defeat")}");
        hourglassView?.SetResultText(evt.PlayerWon);
        SetAllButtonsInteractable(false);
        RefreshViews();
    }

    private void OnCombatBasicInput(CombatStrikeInputEvent evt) => TryRequestFromInput(actionPanelView != null ? actionPanelView.BasicButton : null, RequestBasicAction);
    private void OnCombatSpecialInput(CombatPierceInputEvent evt) => TryRequestFromInput(actionPanelView != null ? actionPanelView.SpecialButton : null, RequestSpecialAction);
    private void OnCombatGuardSkillInput(CombatHexInputEvent evt) => TryRequestFromInput(actionPanelView != null ? actionPanelView.GuardButton : null, RequestGuardAction);
    private void OnCombatGuardInput(CombatGuardInputEvent evt) => TryRequestFromInput(actionPanelView != null ? actionPanelView.FlipButton : null, RequestFlip);

    private void TryRequestFromInput(Button button, System.Action request)
    {
        if (hourglassView != null && hourglassView.IsTransitioning)
        {
            return;
        }

        if (button == null || request == null || !button.interactable)
        {
            return;
        }

        request.Invoke();
    }

    private void RequestBasicAction()
    {
        RequestPlayerAction(_basicAction);
    }

    private void RequestSpecialAction()
    {
        RequestPlayerAction(_specialAction);
    }

    private void RequestGuardAction()
    {
        RequestPlayerAction(_guardAction);
    }

    private void RequestFlip()
    {
        if (hourglassView != null && hourglassView.IsTransitioning) return;
        CacheCombatManager();
        ClearPendingTargeting();
        _combatManager?.RequestEndTurn();
    }

    private void RequestPlayerAction(CombatActionDataSO action)
    {
        if (hourglassView != null && hourglassView.IsTransitioning)
        {
            return;
        }

        CacheCombatManager();
        if (_combatManager == null || action == null || _combatManager.RuntimeState == null)
        {
            return;
        }

        CombatRuntimeState state = _combatManager.RuntimeState;
        if (state.TurnState != CombatTurnState.PlayerCommand || state.IsCombatEnded)
        {
            return;
        }

        CombatActorRuntime source = state.GetSelectedAlly();
        if (source == null || source.IsDead)
        {
            return;
        }

        if (RequiresExplicitEnemyTarget(action))
        {
            _pendingEnemyTargetAction = action;
            _pendingSourceActorId = source.ActorId;
            combatLogView?.AddLog($"[R{state.TurnIndex}] Targeting {action.displayName}");
            RefreshViews(false);
            return;
        }

        ExecuteImmediatePlayerAction(action, -1);
    }

    private bool TryResolvePendingEnemyTargetAction(int enemyActorId)
    {
        if (_pendingEnemyTargetAction == null || _combatManager == null || _combatManager.RuntimeState == null)
        {
            return false;
        }

        CombatRuntimeState state = _combatManager.RuntimeState;
        if (state.TurnState != CombatTurnState.PlayerCommand || state.IsCombatEnded)
        {
            ClearPendingTargeting();
            return false;
        }

        CombatActorRuntime source = state.GetSelectedAlly();
        if (source == null || source.IsDead || source.ActorId != _pendingSourceActorId)
        {
            ClearPendingTargeting();
            RefreshViews(true);
            return true;
        }

        ExecuteImmediatePlayerAction(_pendingEnemyTargetAction, enemyActorId);
        return true;
    }

    private void ExecuteImmediatePlayerAction(CombatActionDataSO action, int targetActorId)
    {
        if (_combatManager == null || action == null)
        {
            return;
        }

        bool ok = _combatManager.TryExecuteActionForSelectedAlly(action, targetActorId, out string reason);
        if (!ok && !string.IsNullOrWhiteSpace(reason))
        {
            combatLogView?.AddLog($"ActionBlocked: {reason}");
        }

        ClearPendingTargeting();
        RefreshViews(true);
    }

    private static bool RequiresExplicitEnemyTarget(CombatActionDataSO action)
    {
        return action != null && action.targetType == CombatTargetType.SingleEnemy;
    }

    private void ClearPendingTargeting()
    {
        _pendingEnemyTargetAction = null;
        _pendingSourceActorId = -1;
    }
}
