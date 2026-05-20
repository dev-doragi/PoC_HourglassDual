using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Data container for combat actor base stats and behavior references.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Combat Actor Data", fileName = "CombatActorData")]
public class CombatActorDataSO : ScriptableObject
{
    [Header("Identity")]
    public int actorId;
    public string displayName;
    [FormerlySerializedAs("actorType")] public CombatActorType teamType;
    public CombatRoleType roleType;
    public int slotIndex;

    [Header("Stats")]
    [FormerlySerializedAs("maxHp")] public int maxHP = 10;
    [FormerlySerializedAs("baseGuard")] public int maxGuard = 3;
    public int initialGuard = 0;
    public int speedBase = 1;

    [Header("Actions / Intents")]
    public CombatActionDataSO[] actionList;
    public EnemyIntentDataSO[] enemyIntentList;

    [Header("Legacy")]
    [FormerlySerializedAs("initialSand")] public int legacyInitialSand = 0;

    public Sprite idleSprite;
    public Sprite attackSprite;
    public Sprite hitSprite;
    public Sprite groggySprite;
    public Sprite deathSprite;
    public Sprite illustrationSprite;

    public Vector3 attackMoveOffset = new Vector3(0.35f, 0f, 0f);
    public float attackMoveDuration = 0.12f;
    public float attackReturnDuration = 0.14f;
    public float hitShakeDuration = 0.18f;
    public float hitShakeStrength = 0.12f;
    public int hitShakeVibrato = 12;
    public float deathFadeDuration = 0.7f;
}
