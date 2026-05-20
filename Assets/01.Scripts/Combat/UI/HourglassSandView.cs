using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays shared hourglass state for 3v3 combat.
/// </summary>
public class HourglassSandView : MonoBehaviour
{
    [SerializeField] private RectTransform _rotatingVisualRoot;
    [SerializeField] private Slider _upperSlider;
    [SerializeField] private Slider _downerSlider;
    [SerializeField] private TMP_Text _upperText;
    [SerializeField] private TMP_Text _downerText;
    [SerializeField] private TMP_Text _turnText;
    [SerializeField] private TMP_Text _nextSandText;
    [SerializeField] private float _flipDuration = 0.45f;
    [SerializeField] private float _flipAnglePerTurn = -180f;

    private bool _isFlipped;
    private bool _isFlipTransitionRunning;
    private Coroutine _flipRoutine;
    private bool _flipQueued;
    private CombatRuntimeState _queuedState;

    public bool IsTransitioning => _isFlipTransitionRunning;

    private void OnDestroy()
    {
        _upperSlider?.DOKill();
        _downerSlider?.DOKill();
        _rotatingVisualRoot?.DOKill();
    }

    public void Refresh(CombatRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        if (_flipQueued && !_isFlipTransitionRunning)
        {
            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
            }

            _flipRoutine = StartCoroutine(FlipRoutine(state));
            return;
        }

        ApplyState(state);
    }

    public void SetFlipDuration(float duration)
    {
        _flipDuration = Mathf.Clamp(duration, 0.1f, 1.5f);
    }

    public void QueueFlipPreview(in CombatLogSnapshot snapshot, CombatRuntimeState state)
    {
        _flipQueued = true;
        _queuedState = state;
    }

    public void SetResultText(bool playerWon)
    {
        if (_turnText != null)
        {
            _turnText.text = playerWon ? "VICTORY" : "DEFEAT";
        }
    }

    private IEnumerator FlipRoutine(CombatRuntimeState state)
    {
        _isFlipTransitionRunning = true;

        if (_rotatingVisualRoot != null)
        {
            _rotatingVisualRoot.DOKill();
            Tween tween = _rotatingVisualRoot.DOLocalRotate(new Vector3(0f, 0f, _flipAnglePerTurn), Mathf.Clamp(_flipDuration, 0.1f, 1f), RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutCubic);
            yield return tween.WaitForCompletion();
        }

        _isFlipped = !_isFlipped;
        _flipQueued = false;
        _isFlipTransitionRunning = false;

        ApplyState(_queuedState != null ? _queuedState : state);
        _queuedState = null;
    }

    private void ApplyState(CombatRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        int unlockedSand = Mathf.Max(1, state.TotalSand - state.LockedSand);

        if (_upperSlider != null)
        {
            _upperSlider.minValue = 0f;
            _upperSlider.maxValue = unlockedSand;
            _upperSlider.value = Mathf.Clamp(state.UpperSand, 0, unlockedSand);
            _upperSlider.interactable = false;
        }

        if (_downerSlider != null)
        {
            _downerSlider.minValue = 0f;
            _downerSlider.maxValue = unlockedSand;
            _downerSlider.value = Mathf.Clamp(state.LowerSand, 0, unlockedSand);
            _downerSlider.interactable = false;
        }

        if (_upperText != null)
        {
            _upperText.text = state.UpperSand.ToString();
        }

        if (_downerText != null)
        {
            _downerText.text = state.LowerSand.ToString();
        }

        if (_turnText != null)
        {
            if (state.TurnState == CombatTurnState.RoundStart)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Start";
            }
            else if (state.TurnState == CombatTurnState.PlayerCommand)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Command";
            }
            else if (state.TurnState == CombatTurnState.PlayerResolving)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Allies";
            }
            else if (state.TurnState == CombatTurnState.Flipping)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Flip";
            }
            else if (state.TurnState == CombatTurnState.EnemyResolving)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Enemies";
            }
            else if (state.TurnState == CombatTurnState.Ended)
            {
                _turnText.text = $"ROUND {state.TurnIndex} / Ended";
            }
            else
            {
                _turnText.text = "-";
            }
        }

        if (_nextSandText != null)
        {
            int predictedEnemySand = Mathf.Max(state.MinimumFall, state.LowerSand + state.PlayerSpend);
            _nextSandText.text = $"MinFall:{state.MinimumFall}  PredEnemySand:{predictedEnemySand}  Pressure:{state.Pressure}";
        }
    }
}
