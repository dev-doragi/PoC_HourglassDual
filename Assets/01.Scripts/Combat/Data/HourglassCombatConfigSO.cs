using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shared combat configuration values for 3v3 hourglass combat.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Hourglass Combat Config", fileName = "HourglassCombatConfig")]
public class HourglassCombatConfigSO : ScriptableObject
{
    [Header("Hourglass")]
    [FormerlySerializedAs("maxTransferSand")] public int totalSand = 10;
    [Range(0, 30)] public int lockedSand = 0;

    [Header("Difficulty / Party")]
    public CombatDifficultyDataSO selectedDifficulty;
    public CombatActorDataSO[] allyPartyActors;
    public CombatActorDataSO[] enemyPartyActors;
    public int defaultSelectedAllyIndex;
    public int defaultSelectedEnemyIndex;

    [Header("Round Bonus")]
    public bool enableKillBonus = true;
    [Range(0, 3)] public int maxKillBonusPerRound = 1;

    [Header("Legacy (unused in v1.1)")]
    [FormerlySerializedAs("minimumTurnSand")] public int legacyMinimumFall = 3;
    public int legacyDefaultPlayerSand = 5;
    public int legacyDefaultEnemySand = 5;
}
