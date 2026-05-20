using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Data container for combat actions (player and enemy fallback actions).
/// </summary>
[CreateAssetMenu(menuName = "Combat/Combat Action Data", fileName = "CombatActionData")]
public class CombatActionDataSO : ScriptableObject
{
    [Header("Identity")]
    public string actionId;
    public string displayName;

    [Header("Action")]
    public CombatActionType actionType;
    public CombatTargetType targetType = CombatTargetType.SingleEnemy;
    [FormerlySerializedAs("sandCost")] public int baseCost = 1;
    public int speed = 1;

    [Header("Effect")]
    [FormerlySerializedAs("baseDamage")] public int hpDamage;
    [FormerlySerializedAs("breakPower")] public int guardDamage;
    [FormerlySerializedAs("guardValue")] public int guardGain;
    public int healAmount;

    [Header("Fallback")]
    public CombatActionType fallbackActionType = CombatActionType.None;

    [Header("Flags")]
    public bool canTargetAlly;
    public bool canTargetEnemy = true;
    public bool isAreaAction;

    [FormerlySerializedAs("prepGain")] public int legacyThreatDelta;
}
