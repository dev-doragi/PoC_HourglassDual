using UnityEngine;

public class CombatBoardView : MonoBehaviour
{
    [SerializeField] private CombatActorSlotView[] _allySlots;
    [SerializeField] private CombatActorSlotView[] _enemySlots;

    private HourglassCombatManager _manager;

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

        RefreshSide(_allySlots, state.Allies, state.SelectedAllySlot);
        RefreshSide(_enemySlots, state.Enemies, state.SelectedEnemySlot);
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
                slots[i].Bind(teamType, i, OnSlotSelected);
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

        _manager.SelectEnemyBySlot(slotIndex);
    }

    private static void RefreshSide(CombatActorSlotView[] slots, System.Collections.Generic.List<CombatActorRuntime> actors, int selectedSlot)
    {
        if (slots == null || actors == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            CombatActorRuntime actor = i < actors.Count ? actors[i] : null;
            slots[i]?.ApplyActor(actor, i == selectedSlot);
        }
    }
}
