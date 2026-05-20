using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays shared hourglass state and flip animation for 3v3 combat.
/// </summary>
public class HourglassSandView : MonoBehaviour
{
    [SerializeField] private RectTransform _rotatingVisualRoot;
    [SerializeField] private CanvasGroup _hourglassTextCanvasGroup;
    [SerializeField] private Slider _upperSlider;
    [SerializeField] private Slider _downerSlider;
    [SerializeField] private TMP_Text _upperText;
    [SerializeField] private TMP_Text _downerText;
    [SerializeField] private TMP_Text _turnText;
    [SerializeField] private TMP_Text _nextSandText;
    [SerializeField] private float _flipDuration = 0.45f;
    [SerializeField] private float _flipAnglePerTurn = -180f;
    [SerializeField] private float _textFadeDuration = 0.08f;
    [Header("Sand Tween")]
    [SerializeField] private float _sandTweenDuration = 0.16f;
    [SerializeField] private Ease _sandTweenEase = Ease.Linear;

    private bool _isFlipped;
    private bool _isFlipTransitionRunning;
    private bool _hasPendingFlipPreview;
    private CombatTurnState _previewTurnState;
    private CombatRuntimeState _queuedStateDuringFlip;
    private Coroutine _flipRoutine;
    private Slider.Direction _upperBaseDirection;
    private Slider.Direction _downerBaseDirection;
    private int _runningSandTweens;

    public bool IsTransitioning => _isFlipTransitionRunning;

    private void Awake()
    {
        _upperBaseDirection = _upperSlider != null ? _upperSlider.direction : Slider.Direction.BottomToTop;
        _downerBaseDirection = _downerSlider != null ? _downerSlider.direction : Slider.Direction.BottomToTop;
        ApplySliderDirection();
        if (_hourglassTextCanvasGroup != null)
        {
            _hourglassTextCanvasGroup.alpha = 1f;
        }
    }

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

        if (_isFlipTransitionRunning)
        {
            _queuedStateDuringFlip = state;
            return;
        }

        if (_hasPendingFlipPreview && _flipRoutine == null)
        {
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
        _hasPendingFlipPreview = true;
        _previewTurnState = snapshot.turn_state;
        _queuedStateDuringFlip = state;
    }

    public void AnimateQueuedSpend(int predictedUpper, int predictedLower)
    {
        int max = _upperSlider != null ? Mathf.RoundToInt(_upperSlider.maxValue) : Mathf.Max(predictedUpper, predictedLower);
        ApplySandState(predictedUpper, predictedLower, Mathf.Max(1, max), false);
    }

    public void AnimateMinimumFall(int upperAfter, int lowerAfter, int forcedAmount)
    {
        if (forcedAmount <= 0)
        {
            return;
        }

        int max = _upperSlider != null ? Mathf.RoundToInt(_upperSlider.maxValue) : Mathf.Max(upperAfter, lowerAfter);
        ApplySandState(upperAfter, lowerAfter, Mathf.Max(1, max), false);
    }

    public void SetResultText(bool playerWon)
    {
        if (_turnText != null)
        {
            _turnText.text = playerWon ? "VICTORY" : "DEFEAT";
        }
    }

    private IEnumerator FlipRoutine(CombatRuntimeState fallbackState)
    {
        _isFlipTransitionRunning = true;

        while (_runningSandTweens > 0)
        {
            yield return null;
        }

        yield return FadeStaticTexts(0f);

        if (_rotatingVisualRoot != null)
        {
            _rotatingVisualRoot.DOKill();
            Tween tween = _rotatingVisualRoot.DOLocalRotate(new Vector3(0f, 0f, _flipAnglePerTurn), Mathf.Clamp(_flipDuration, 0.1f, 1f), RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutCubic);
            yield return tween.WaitForCompletion();
        }

        _isFlipped = !_isFlipped;
        ApplySliderDirection();

        CombatRuntimeState stateToApply = _queuedStateDuringFlip != null ? _queuedStateDuringFlip : fallbackState;
        _queuedStateDuringFlip = null;
        _hasPendingFlipPreview = false;
        if (stateToApply != null)
        {
            ApplyTextState(stateToApply, stateToApply.LowerSand);
        }

        yield return FadeStaticTexts(1f);

        _isFlipTransitionRunning = false;
        _flipRoutine = null;
    }

    private void ApplyState(CombatRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        int maxSand = Mathf.Max(1, state.TotalSand - state.LockedSand);
        int upper = Mathf.Clamp(state.UpperSand, 0, maxSand);
        int lower = Mathf.Clamp(state.LowerSand, 0, maxSand);

        ApplySandState(upper, lower, maxSand, true);
        ApplyTextState(state, lower);
    }

    private void SetTurnText(CombatTurnState turnState)
    {
        if (_turnText == null)
        {
            return;
        }

        if (IsPlayerTurnState(turnState))
        {
            _turnText.text = "PLAYER TURN";
        }
        else if (turnState == CombatTurnState.EnemyResolving)
        {
            _turnText.text = "ENEMY TURN";
        }
        else if (turnState == CombatTurnState.Flipping)
        {
            _turnText.text = "FLIP";
        }
        else
        {
            _turnText.text = "-";
        }
    }

    private static bool IsPlayerTurnState(CombatTurnState state)
    {
        return state == CombatTurnState.RoundStart
            || state == CombatTurnState.PlayerCommand
            || state == CombatTurnState.PlayerResolving;
    }

    private void SetNextSandText(CombatRuntimeState state, int currentLower)
    {
        if (_nextSandText == null || state == null)
        {
            return;
        }

        int predictedEnemySand = Mathf.Max(state.MinimumFall, currentLower);
        _nextSandText.text = predictedEnemySand.ToString();
    }

    private void ApplySliderDirection()
    {
        if (_upperSlider != null)
        {
            _upperSlider.direction = _isFlipped ? ToggleDirection(_upperBaseDirection) : _upperBaseDirection;
        }

        if (_downerSlider != null)
        {
            _downerSlider.direction = _isFlipped ? ToggleDirection(_downerBaseDirection) : _downerBaseDirection;
        }
    }

    private static Slider.Direction ToggleDirection(Slider.Direction source)
    {
        switch (source)
        {
            case Slider.Direction.BottomToTop:
                return Slider.Direction.TopToBottom;
            case Slider.Direction.TopToBottom:
                return Slider.Direction.BottomToTop;
            case Slider.Direction.LeftToRight:
                return Slider.Direction.RightToLeft;
            case Slider.Direction.RightToLeft:
                return Slider.Direction.LeftToRight;
            default:
                return source;
        }
    }

    private void ApplySandState(int upper, int lower, int maxSand, bool immediate)
    {
        Slider topVisibleSlider = GetTopVisibleSlider();
        Slider bottomVisibleSlider = GetBottomVisibleSlider();

        if (topVisibleSlider != null)
        {
            topVisibleSlider.minValue = 0f;
            topVisibleSlider.maxValue = maxSand;
            topVisibleSlider.interactable = false;
            TweenSliderValue(topVisibleSlider, upper, immediate);
        }

        if (bottomVisibleSlider != null)
        {
            bottomVisibleSlider.minValue = 0f;
            bottomVisibleSlider.maxValue = maxSand;
            bottomVisibleSlider.interactable = false;
            TweenSliderValue(bottomVisibleSlider, lower, immediate);
        }
    }

    private void TweenSliderValue(Slider slider, int targetValue, bool immediate)
    {
        if (slider == null)
        {
            return;
        }

        float clampedTarget = Mathf.Clamp(targetValue, slider.minValue, slider.maxValue);
        slider.DOKill();

        float duration = immediate ? 0f : Mathf.Max(0f, _sandTweenDuration);
        if (duration <= 0f || Mathf.Approximately(slider.value, clampedTarget))
        {
            slider.value = clampedTarget;
            return;
        }

        _runningSandTweens++;
        slider.DOValue(clampedTarget, duration)
            .SetEase(_sandTweenEase)
            .OnComplete(() => _runningSandTweens = Mathf.Max(0, _runningSandTweens - 1))
            .OnKill(() => _runningSandTweens = Mathf.Max(0, _runningSandTweens - 1));
    }

    private Slider GetTopVisibleSlider()
    {
        return _isFlipped ? _downerSlider : _upperSlider;
    }

    private Slider GetBottomVisibleSlider()
    {
        return _isFlipped ? _upperSlider : _downerSlider;
    }

    private void ApplyTextState(CombatRuntimeState state, int lower)
    {
        if (_upperText != null)
        {
            _upperText.text = Mathf.Max(0, state.UpperSand).ToString();
        }

        if (_downerText != null)
        {
            _downerText.text = Mathf.Max(0, lower).ToString();
        }

        SetTurnText(state.TurnState);
        SetNextSandText(state, lower);
    }

    private IEnumerator FadeStaticTexts(float targetAlpha)
    {
        float duration = Mathf.Clamp(_textFadeDuration, 0.01f, 0.2f);

        if (_hourglassTextCanvasGroup != null)
        {
            _hourglassTextCanvasGroup.DOKill();
            _hourglassTextCanvasGroup.DOFade(targetAlpha, duration).SetEase(Ease.OutQuad);
        }

        FadeLabel(_turnText, targetAlpha, duration);
        FadeLabel(_nextSandText, targetAlpha, duration);

        yield return new WaitForSeconds(duration);
    }

    private static void FadeLabel(TMP_Text text, float targetAlpha, float duration)
    {
        if (text == null)
        {
            return;
        }

        text.DOKill();
        text.DOFade(targetAlpha, duration).SetEase(Ease.OutQuad);
    }
}
