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
    [SerializeField] private float _sandMoveDuration = 0.2f;
    [SerializeField] private float _minimumFallDuration = 0.25f;

    private bool _isFlipped;
    private bool _isFlipTransitionRunning;
    private Coroutine _flipRoutine;
    private bool _flipQueued;
    private CombatRuntimeState _queuedState;
    private bool _isSandAnimating;
    private Tween _upperTween;
    private Tween _lowerTween;

    public bool IsTransitioning => _isFlipTransitionRunning;

    private void OnDestroy()
    {
        _upperTween?.Kill();
        _lowerTween?.Kill();
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

    public void AnimateQueuedSpend(int predictedUpper, int predictedLower)
    {
        AnimateSandTo(predictedUpper, predictedLower, Mathf.Max(0.05f, _sandMoveDuration));
    }

    public void AnimateMinimumFall(int upperAfter, int lowerAfter, int forcedAmount)
    {
        if (forcedAmount <= 0)
        {
            return;
        }

        AnimateSandTo(upperAfter, lowerAfter, Mathf.Max(0.08f, _minimumFallDuration));
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
        while (_isSandAnimating)
        {
            yield return null;
        }

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
            if (!_isSandAnimating)
            {
                _upperSlider.value = Mathf.Clamp(state.UpperSand, 0, unlockedSand);
            }
            _upperSlider.interactable = false;
        }

        if (_downerSlider != null)
        {
            _downerSlider.minValue = 0f;
            _downerSlider.maxValue = unlockedSand;
            if (!_isSandAnimating)
            {
                _downerSlider.value = Mathf.Clamp(state.LowerSand, 0, unlockedSand);
            }
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
                _turnText.text = "PLAYER TURN";
            }
            else if (state.TurnState == CombatTurnState.PlayerCommand)
            {
                _turnText.text = "PLAYER TURN";
            }
            else if (state.TurnState == CombatTurnState.PlayerResolving)
            {
                _turnText.text = "PLAYER TURN";
            }
            else if (state.TurnState == CombatTurnState.Flipping)
            {
                _turnText.text = "FLIP";
            }
            else if (state.TurnState == CombatTurnState.EnemyResolving)
            {
                _turnText.text = "ENEMY TURN";
            }
            else if (state.TurnState == CombatTurnState.Ended)
            {
                _turnText.text = "ENDED";
            }
            else
            {
                _turnText.text = "-";
            }
        }

        if (_nextSandText != null)
        {
            int predictedEnemySand = Mathf.Max(state.MinimumFall, state.LowerSand + state.PlayerSpend);
            _nextSandText.text = predictedEnemySand.ToString();
        }
    }

    private void AnimateSandTo(int upper, int lower, float duration)
    {
        if (_upperSlider == null || _downerSlider == null)
        {
            return;
        }

        _upperTween?.Kill();
        _lowerTween?.Kill();

        _isSandAnimating = true;
        _upperTween = _upperSlider.DOValue(upper, duration).SetEase(Ease.OutQuad);
        _lowerTween = _downerSlider.DOValue(lower, duration).SetEase(Ease.OutQuad);
        _lowerTween.OnComplete(() =>
        {
            _isSandAnimating = false;
            if (_upperText != null)
            {
                _upperText.text = Mathf.RoundToInt(_upperSlider.value).ToString();
            }

            if (_downerText != null)
            {
                _downerText.text = Mathf.RoundToInt(_downerSlider.value).ToString();
            }
        });
    }
}
