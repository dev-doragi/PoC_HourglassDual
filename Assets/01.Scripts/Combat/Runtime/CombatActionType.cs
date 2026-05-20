public enum CombatActionType
{
    None = 0,

    // Player-facing actions.
    BasicAttack = 1,
    HeavyStrike = 2,
    BreakAttack = 3,
    Pierce = 4,
    Guard = 5,
    TeamWard = 6,

    // Utility.
    EndTurn = 10,
    NoAction = 11,

    // Enemy actions/intents.
    HeavyBlow = 20,
    LightBlow = 21,
    GroupHeal = 22,
    Explosion = 23,
    Spark = 24,

    // Legacy aliases kept for old assets/scripts.
    Strike = BasicAttack,
    Hex = TeamWard,
    EnemyBruiserSmash = HeavyBlow,
    EnemyHealerRestore = GroupHeal,
    EnemyCasterBurst = Explosion
}
