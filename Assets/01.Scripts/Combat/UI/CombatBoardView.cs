using UnityEngine;

public class CombatBoardView : MonoBehaviour
{
    [SerializeField] private CombatActorSlotView[] _allySlots;
    [SerializeField] private CombatActorSlotView[] _enemySlots;

    private HourglassCombatManager _manager;
    private bool _targetingModeEnabled;
    private int _hoverEnemySlot = -1;
    private int _lockedEnemySlot = -1;

    public void Bind(HourglassCombatManager manager)
    {
        _manager = manager;
        BindSlots(_allySlots, CombatActorType.Ally);
        BindSlots(_enemySlots, CombatActorType.Enemy);
    }

    public void Refresh(CombatRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        RefreshSide(_allySlots, CombatActorType.Ally, state.Allies, state.SelectedAllySlot);
        RefreshSide(_enemySlots, CombatActorType.Enemy, state.Enemies, state.SelectedEnemySlot);
    }

    public void SetTargetingMode(bool enabled, int lockedEnemySlot = -1)
    {
        int normalizedLocked = enabled ? Mathf.Max(-1, lockedEnemySlot) : -1;
        if (_targetingModeEnabled == enabled && _lockedEnemySlot == normalizedLocked)
        {
            ApplyEnemyTargetHighlights();
            return;
        }

        _targetingModeEnabled = enabled;
        if (!enabled)
        {
            _hoverEnemySlot = -1;
        }

        _lockedEnemySlot = normalizedLocked;
        ApplyEnemyTargetHighlights();
    }

    public void SetTargetLockedSlot(int enemySlot)
    {
        if (!_targetingModeEnabled)
        {
            return;
        }

        _lockedEnemySlot = enemySlot;
        ApplyEnemyTargetHighlights();
    }

    public void ClearTargetingMode()
    {
        SetTargetingMode(false, -1);
    }

    private void BindSlots(CombatActorSlotView[] slots, CombatActorType teamType)
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].Bind(teamType, i, OnSlotSelected, OnSlotHoverChanged);
            }
        }
    }

    private void OnSlotSelected(CombatActorType teamType, int slotIndex)
    {
        if (_manager == null)
        {
            return;
        }

        if (teamType == CombatActorType.Ally)
        {
            _manager.SelectAllyBySlot(slotIndex);
            return;
        }

        if (_targetingModeEnabled)
        {
            _lockedEnemySlot = slotIndex;
            ApplyEnemyTargetHighlights();
        }

        _manager.SelectEnemyBySlot(slotIndex);
    }

    private void OnSlotHoverChanged(CombatActorType teamType, int slotIndex, bool hovered)
    {
        if (!_targetingModeEnabled || teamType != CombatActorType.Enemy)
        {
            return;
        }

        if (hovered)
        {
            _hoverEnemySlot = slotIndex;
        }
        else if (_hoverEnemySlot == slotIndex)
        {
            _hoverEnemySlot = -1;
        }

        ApplyEnemyTargetHighlights();
    }

    private void RefreshSide(
        CombatActorSlotView[] slots,
        CombatActorType teamType,
        System.Collections.Generic.List<CombatActorRuntime> actors,
        int selectedSlot)
    {
        if (slots == null || actors == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            CombatActorSlotView slotView = slots[i];
            if (slotView == null)
            {
                continue;
            }

            CombatActorRuntime actor = i < actors.Count ? actors[i] : null;
            slotView.ApplyActor(actor, i == selectedSlot);

            if (teamType != CombatActorType.Enemy)
            {
                slotView.SetTargetingMode(false);
                slotView.ClearTargetHighlight();
                continue;
            }

            bool targetable = _targetingModeEnabled && actor != null && !actor.IsDead;
            bool locked = targetable && i == _lockedEnemySlot;
            bool hover = targetable && i == _hoverEnemySlot && !locked;
            slotView.SetTargetingMode(_targetingModeEnabled);
            slotView.SetTargetable(targetable);
            slotView.SetTargetHover(hover);
            slotView.SetTargetLocked(locked);
        }
    }

    private void ApplyEnemyTargetHighlights()
    {
        CombatRuntimeState state = _manager != null ? _manager.RuntimeState : null;
        if (state == null || _enemySlots == null)
        {
            return;
        }

        RefreshSide(_enemySlots, CombatActorType.Enemy, state.Enemies, state.SelectedEnemySlot);
    }
}
