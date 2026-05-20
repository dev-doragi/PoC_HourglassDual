public readonly struct CombatActorSnapshot
{
    public readonly int actor_id;
    public readonly string actor_name;
    public readonly CombatActorType actor_type;
    public readonly int slot_index;
    public readonly int hp;
    public readonly int max_hp;
    public readonly int guard;
    public readonly int max_guard;
    public readonly bool is_dead;
    public readonly bool skip_next;

    public CombatActorSnapshot(
        int actorId,
        string actorName,
        CombatActorType actorType,
        int slotIndex,
        int hp,
        int maxHp,
        int guard,
        int maxGuard,
        bool isDead,
        bool skipNext)
    {
        actor_id = actorId;
        actor_name = actorName;
        actor_type = actorType;
        slot_index = slotIndex;
        this.hp = hp;
        max_hp = maxHp;
        this.guard = guard;
        max_guard = maxGuard;
        is_dead = isDead;
        skip_next = skipNext;
    }
}

public readonly struct CombatIntentSnapshot
{
    public readonly int source_actor_id;
    public readonly int source_slot_index;
    public readonly CombatActionType action_type;
    public readonly int target_actor_id;
    public readonly int base_cost;
    public readonly int effective_cost;
    public readonly int speed;
    public readonly CombatActionType fallback_action_type;

    public CombatIntentSnapshot(
        int sourceActorId,
        int sourceSlotIndex,
        CombatActionType actionType,
        int targetActorId,
        int baseCost,
        int effectiveCost,
        int speed,
        CombatActionType fallbackActionType)
    {
        source_actor_id = sourceActorId;
        source_slot_index = sourceSlotIndex;
        action_type = actionType;
        target_actor_id = targetActorId;
        base_cost = baseCost;
        effective_cost = effectiveCost;
        this.speed = speed;
        fallback_action_type = fallbackActionType;
    }
}

public readonly struct CombatTimelineEntrySnapshot
{
    public readonly int timeline_index;
    public readonly CombatTimelineSide side;
    public readonly CombatTimelineEntryType entry_type;
    public readonly int source_actor_id;
    public readonly int source_slot_index;
    public readonly int target_actor_id;
    public readonly CombatActionType action_type;
    public readonly int cost;
    public readonly int speed;
    public readonly string label;
    public readonly string status;

    public CombatTimelineEntrySnapshot(
        int timelineIndex,
        CombatTimelineSide side,
        CombatTimelineEntryType entryType,
        int sourceActorId,
        int sourceSlotIndex,
        int targetActorId,
        CombatActionType actionType,
        int cost,
        int speed,
        string label,
        string status)
    {
        timeline_index = timelineIndex;
        this.side = side;
        entry_type = entryType;
        source_actor_id = sourceActorId;
        source_slot_index = sourceSlotIndex;
        target_actor_id = targetActorId;
        action_type = actionType;
        this.cost = cost;
        this.speed = speed;
        this.label = label;
        this.status = status;
    }
}

public readonly struct CombatLogSnapshot
{
    public readonly int round_index;
    public readonly CombatTurnState turn_state;
    public readonly int upper_sand;
    public readonly int lower_sand;
    public readonly int player_spend;
    public readonly int enemy_sand;
    public readonly int minimum_fall;
    public readonly int pressure;
    public readonly int kill_bonus_token;
    public readonly int selected_ally_slot;
    public readonly int selected_enemy_slot;
    public readonly CombatActorSnapshot[] allies;
    public readonly CombatActorSnapshot[] enemies;
    public readonly CombatIntentSnapshot[] intents;
    public readonly CombatTimelineEntrySnapshot[] timeline;

    public CombatLogSnapshot(
        int roundIndex,
        CombatTurnState turnState,
        int upperSand,
        int lowerSand,
        int playerSpend,
        int enemySand,
        int minimumFall,
        int pressure,
        int killBonusToken,
        int selectedAllySlot,
        int selectedEnemySlot,
        CombatActorSnapshot[] allies,
        CombatActorSnapshot[] enemies,
        CombatIntentSnapshot[] intents,
        CombatTimelineEntrySnapshot[] timeline)
    {
        round_index = roundIndex;
        turn_state = turnState;
        upper_sand = upperSand;
        lower_sand = lowerSand;
        player_spend = playerSpend;
        enemy_sand = enemySand;
        minimum_fall = minimumFall;
        this.pressure = pressure;
        kill_bonus_token = killBonusToken;
        selected_ally_slot = selectedAllySlot;
        selected_enemy_slot = selectedEnemySlot;
        this.allies = allies ?? System.Array.Empty<CombatActorSnapshot>();
        this.enemies = enemies ?? System.Array.Empty<CombatActorSnapshot>();
        this.intents = intents ?? System.Array.Empty<CombatIntentSnapshot>();
        this.timeline = timeline ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
    }
}

public readonly struct CombatRoundStartedEvent
{
    public readonly CombatLogSnapshot Snapshot;

    public CombatRoundStartedEvent(CombatLogSnapshot snapshot)
    {
        Snapshot = snapshot;
    }
}

public readonly struct CombatIntentShownEvent
{
    public readonly CombatIntentSnapshot Intent;
    public readonly CombatLogSnapshot Snapshot;

    public CombatIntentShownEvent(CombatIntentSnapshot intent, CombatLogSnapshot snapshot)
    {
        Intent = intent;
        Snapshot = snapshot;
    }
}

public readonly struct CombatCommandQueuedEvent
{
    public readonly CombatCommandRuntime Command;
    public readonly int PredictedUpperSand;
    public readonly int PredictedLowerSand;
    public readonly CombatLogSnapshot Snapshot;

    public CombatCommandQueuedEvent(CombatCommandRuntime command, int predictedUpperSand, int predictedLowerSand, CombatLogSnapshot snapshot)
    {
        Command = command;
        PredictedUpperSand = predictedUpperSand;
        PredictedLowerSand = predictedLowerSand;
        Snapshot = snapshot;
    }
}

public readonly struct CombatCommandConfirmedEvent
{
    public readonly int PlayerSpend;
    public readonly CombatLogSnapshot Snapshot;

    public CombatCommandConfirmedEvent(int playerSpend, CombatLogSnapshot snapshot)
    {
        PlayerSpend = playerSpend;
        Snapshot = snapshot;
    }
}

public readonly struct CombatAllyCommandResolvedEvent
{
    public readonly CombatCommandRuntime Command;
    public readonly bool Succeeded;
    public readonly string Message;
    public readonly CombatLogSnapshot Snapshot;

    public CombatAllyCommandResolvedEvent(CombatCommandRuntime command, bool succeeded, string message, CombatLogSnapshot snapshot)
    {
        Command = command;
        Succeeded = succeeded;
        Message = message;
        Snapshot = snapshot;
    }
}

public readonly struct CombatMinimumFallAppliedEvent
{
    public readonly int ForcedAmount;
    public readonly int MinimumFall;
    public readonly int UpperAfter;
    public readonly int LowerAfter;
    public readonly CombatLogSnapshot Snapshot;

    public CombatMinimumFallAppliedEvent(int forcedAmount, int minimumFall, int upperAfter, int lowerAfter, CombatLogSnapshot snapshot)
    {
        ForcedAmount = forcedAmount;
        MinimumFall = minimumFall;
        UpperAfter = upperAfter;
        LowerAfter = lowerAfter;
        Snapshot = snapshot;
    }
}

public readonly struct CombatHourglassFlippedEvent
{
    public readonly int EnemySand;
    public readonly CombatLogSnapshot Snapshot;

    public CombatHourglassFlippedEvent(int enemySand, CombatLogSnapshot snapshot)
    {
        EnemySand = enemySand;
        Snapshot = snapshot;
    }
}

public readonly struct CombatTimelineStartedEvent
{
    public readonly CombatTimelineEntrySnapshot[] Timeline;
    public readonly CombatLogSnapshot Snapshot;

    public CombatTimelineStartedEvent(CombatTimelineEntrySnapshot[] timeline, CombatLogSnapshot snapshot)
    {
        Timeline = timeline ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        Snapshot = snapshot;
    }
}

public readonly struct CombatEnemyOrderChangedEvent
{
    public readonly CombatTimelineEntrySnapshot[] EnemyOrder;
    public readonly CombatLogSnapshot Snapshot;

    public CombatEnemyOrderChangedEvent(CombatTimelineEntrySnapshot[] enemyOrder, CombatLogSnapshot snapshot)
    {
        EnemyOrder = enemyOrder ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        Snapshot = snapshot;
    }
}

public readonly struct CombatTimelinePreviewChangedEvent
{
    public readonly CombatTimelineEntrySnapshot[] Timeline;
    public readonly CombatLogSnapshot Snapshot;

    public CombatTimelinePreviewChangedEvent(CombatTimelineEntrySnapshot[] timeline, CombatLogSnapshot snapshot)
    {
        Timeline = timeline ?? System.Array.Empty<CombatTimelineEntrySnapshot>();
        Snapshot = snapshot;
    }
}

public readonly struct CombatEnemyOrderEntryResolvedEvent
{
    public readonly CombatTimelineEntrySnapshot Entry;
    public readonly string Message;
    public readonly CombatLogSnapshot Snapshot;

    public CombatEnemyOrderEntryResolvedEvent(CombatTimelineEntrySnapshot entry, string message, CombatLogSnapshot snapshot)
    {
        Entry = entry;
        Message = message;
        Snapshot = snapshot;
    }
}

public readonly struct CombatTimelineEntryResolvedEvent
{
    public readonly CombatTimelineEntrySnapshot Entry;
    public readonly bool Succeeded;
    public readonly string Message;
    public readonly CombatLogSnapshot Snapshot;

    public CombatTimelineEntryResolvedEvent(CombatTimelineEntrySnapshot entry, bool succeeded, string message, CombatLogSnapshot snapshot)
    {
        Entry = entry;
        Succeeded = succeeded;
        Message = message;
        Snapshot = snapshot;
    }
}

public readonly struct CombatIntentResolvedEvent
{
    public readonly CombatIntentSnapshot Intent;
    public readonly int SpentEnemySand;
    public readonly CombatLogSnapshot Snapshot;

    public CombatIntentResolvedEvent(CombatIntentSnapshot intent, int spentEnemySand, CombatLogSnapshot snapshot)
    {
        Intent = intent;
        SpentEnemySand = spentEnemySand;
        Snapshot = snapshot;
    }
}

public readonly struct CombatIntentFailedEvent
{
    public readonly CombatIntentSnapshot Intent;
    public readonly string Reason;
    public readonly CombatLogSnapshot Snapshot;

    public CombatIntentFailedEvent(CombatIntentSnapshot intent, string reason, CombatLogSnapshot snapshot)
    {
        Intent = intent;
        Reason = reason;
        Snapshot = snapshot;
    }
}

public readonly struct CombatActorSelectedEvent
{
    public readonly CombatActorType TeamType;
    public readonly int SlotIndex;
    public readonly int ActorId;
    public readonly CombatLogSnapshot Snapshot;

    public CombatActorSelectedEvent(CombatActorType teamType, int slotIndex, int actorId, CombatLogSnapshot snapshot)
    {
        TeamType = teamType;
        SlotIndex = slotIndex;
        ActorId = actorId;
        Snapshot = snapshot;
    }
}

public readonly struct CombatActorDamagedEvent
{
    public readonly int ActorId;
    public readonly int Damage;
    public readonly CombatLogSnapshot Snapshot;

    public CombatActorDamagedEvent(int actorId, int damage, CombatLogSnapshot snapshot)
    {
        ActorId = actorId;
        Damage = damage;
        Snapshot = snapshot;
    }
}

public readonly struct CombatActorGuardChangedEvent
{
    public readonly int ActorId;
    public readonly int BeforeGuard;
    public readonly int AfterGuard;
    public readonly CombatLogSnapshot Snapshot;

    public CombatActorGuardChangedEvent(int actorId, int beforeGuard, int afterGuard, CombatLogSnapshot snapshot)
    {
        ActorId = actorId;
        BeforeGuard = beforeGuard;
        AfterGuard = afterGuard;
        Snapshot = snapshot;
    }
}

public readonly struct CombatActorBrokenEvent
{
    public readonly int ActorId;
    public readonly CombatLogSnapshot Snapshot;

    public CombatActorBrokenEvent(int actorId, CombatLogSnapshot snapshot)
    {
        ActorId = actorId;
        Snapshot = snapshot;
    }
}

public readonly struct CombatActorKilledEvent
{
    public readonly int ActorId;
    public readonly CombatLogSnapshot Snapshot;

    public CombatActorKilledEvent(int actorId, CombatLogSnapshot snapshot)
    {
        ActorId = actorId;
        Snapshot = snapshot;
    }
}

public readonly struct CombatKillBonusGrantedEvent
{
    public readonly int ActorId;
    public readonly int KillBonusToken;
    public readonly CombatLogSnapshot Snapshot;

    public CombatKillBonusGrantedEvent(int actorId, int killBonusToken, CombatLogSnapshot snapshot)
    {
        ActorId = actorId;
        KillBonusToken = killBonusToken;
        Snapshot = snapshot;
    }
}

public readonly struct CombatPressureChangedEvent
{
    public readonly int PreviousPressure;
    public readonly int NewPressure;
    public readonly CombatLogSnapshot Snapshot;

    public CombatPressureChangedEvent(int previousPressure, int newPressure, CombatLogSnapshot snapshot)
    {
        PreviousPressure = previousPressure;
        NewPressure = newPressure;
        Snapshot = snapshot;
    }
}

public readonly struct CombatEndedEvent
{
    public readonly bool PlayerWon;
    public readonly CombatLogSnapshot Snapshot;

    public CombatEndedEvent(bool playerWon, CombatLogSnapshot snapshot)
    {
        PlayerWon = playerWon;
        Snapshot = snapshot;
    }
}
