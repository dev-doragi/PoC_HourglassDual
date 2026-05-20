using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Combat Difficulty Data", fileName = "CombatDifficultyData")]
public class CombatDifficultyDataSO : ScriptableObject
{
    public string difficultyName = "Standard";
    public int playerStartSand = 6;
    public int minimumFall = 2;

    [Header("Pressure")]
    public int pressureRound1 = 0;
    public int pressureRound3 = 1;
    public int pressureRound5 = 2;
    public int pressureMax = 2;

    [Header("Enemy")]
    public int enemyCostMin = 1;
}
