using UnityEngine;

/// <summary>
/// Applies hourglass transitions for v1.1 round flow.
/// </summary>
public class CombatTurnProcessor
{
    public readonly struct MinimumFallResult
    {
        public readonly int ForcedFallAmount;
        public readonly int UpperAfter;
        public readonly int LowerAfter;

        public MinimumFallResult(int forcedFallAmount, int upperAfter, int lowerAfter)
        {
            ForcedFallAmount = forcedFallAmount;
            UpperAfter = upperAfter;
            LowerAfter = lowerAfter;
        }
    }

    public MinimumFallResult ApplyMinimumFall(CombatRuntimeState runtimeState)
    {
        if (runtimeState == null)
        {
            return new MinimumFallResult(0, 0, 0);
        }

        int upper = Mathf.Max(0, runtimeState.UpperSand);
        int lower = Mathf.Max(0, runtimeState.LowerSand);
        int minimumFall = Mathf.Max(0, runtimeState.MinimumFall);

        if (minimumFall <= 0 || lower >= minimumFall || upper <= 0)
        {
            return new MinimumFallResult(0, upper, lower);
        }

        int shortage = minimumFall - lower;
        int forced = Mathf.Min(shortage, upper);
        upper -= forced;
        lower += forced;

        runtimeState.UpperSand = upper;
        runtimeState.LowerSand = lower;

        return new MinimumFallResult(forced, upper, lower);
    }

    public void FlipHourglass(CombatRuntimeState runtimeState, int transferredToEnemy)
    {
        if (runtimeState == null)
        {
            return;
        }

        int previousUpper = Mathf.Max(0, runtimeState.UpperSand);
        int previousLower = Mathf.Max(0, runtimeState.LowerSand);

        runtimeState.UpperSand = previousLower;
        runtimeState.LowerSand = previousUpper;
        runtimeState.EnemySand = Mathf.Max(0, transferredToEnemy);
    }

    public static int ComputePressure(CombatDifficultyDataSO difficultyData, int round)
    {
        int safeRound = Mathf.Max(1, round);
        if (difficultyData == null)
        {
            if (safeRound <= 2) return 0;
            if (safeRound <= 4) return 1;
            return 2;
        }

        int pressure = difficultyData.pressureRound1;
        if (safeRound >= 3)
        {
            pressure = difficultyData.pressureRound3;
        }

        if (safeRound >= 5)
        {
            pressure = difficultyData.pressureRound5;
        }

        return Mathf.Clamp(pressure, 0, Mathf.Max(0, difficultyData.pressureMax));
    }
}
