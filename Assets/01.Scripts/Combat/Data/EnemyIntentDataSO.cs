using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Enemy Intent Data", fileName = "EnemyIntentData")]
public class EnemyIntentDataSO : ScriptableObject
{
    [Header("Identity")]
    public string intentId;
    public string displayName;

    [Header("Action")]
    public CombatActionType actionType;
    public CombatTargetType targetRule = CombatTargetType.SingleEnemy;
    public int baseCost = 1;
    public int minEffectiveCost = 1;
    public int speed = 1;

    [Header("Fallback")]
    public CombatActionType fallbackActionType = CombatActionType.None;

    [Header("Effect")]
    public int hpDamage;
    public int guardDamage;
    public int healAmount;
    public bool isAreaAction;
}
