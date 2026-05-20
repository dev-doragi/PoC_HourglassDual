using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders actor status and lightweight feedback animations.
/// </summary>
public class CombatActorPanelView : MonoBehaviour
{
    [SerializeField] private RectTransform _motionRoot;
    [SerializeField] private RectTransform _popupAnchor;
    [SerializeField] private Image _border;
    [SerializeField] private Image _hitFlash;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private TMP_Text _guardText;
    [SerializeField] private Slider _guardBar;
    [SerializeField] private Slider _threatBar;
    [SerializeField] private TMP_Text _groggyText;
    [SerializeField] private TMP_Text _warningText;
    [SerializeField] private TMP_Text[] _turnTintTexts;
    [SerializeField] private Color _turnHighlightColor = new Color(1f, 0.88f, 0.2f, 1f);

    private Vector2 _baseMotionPos;
    private Color _normalBorderColor = Color.white;
    private Color _normalTextColor = Color.white;
    private bool _visualCached;

    public RectTransform PopupAnchor => _popupAnchor != null ? _popupAnchor : transform as RectTransform;

    private void Awake()
    {
        CacheVisualDefaults();
    }

    private void OnDisable()
    {
        _motionRoot?.DOKill();
        _hitFlash?.DOKill();
    }

    public void ApplyActorState(CombatActorRuntime actor, bool isCurrentTurn, bool isAlly)
    {
        CacheVisualDefaults();
        if (actor == null)
        {
            return;
        }

        if (_hpText != null)
        {
            _hpText.text = $"{actor.DisplayName}  HP {actor.CurrentHp}/{Mathf.Max(1, actor.MaxHp)}";
        }

        if (_hpBar != null)
        {
            _hpBar.minValue = 0f;
            _hpBar.maxValue = Mathf.Max(1f, actor.MaxHp);
            _hpBar.value = Mathf.Clamp(actor.CurrentHp, 0, actor.MaxHp);
            _hpBar.interactable = false;
        }

        if (_guardText != null)
        {
            _guardText.text = $"Guard {actor.GuardValue}/{Mathf.Max(0, actor.MaxGuard)}";
        }

        if (_guardBar != null)
        {
            _guardBar.minValue = 0f;
            _guardBar.maxValue = Mathf.Max(1f, actor.MaxGuard);
            _guardBar.value = Mathf.Clamp(actor.GuardValue, 0f, _guardBar.maxValue);
            _guardBar.interactable = false;
        }

        if (_threatBar != null)
        {
            _threatBar.minValue = 0f;
            _threatBar.maxValue = 1f;
            _threatBar.value = 0f;
            _threatBar.interactable = false;
        }

        if (_groggyText != null)
        {
            _groggyText.text = actor.SkipCurrentAction || actor.SkipNextAction ? "Break Skip" : string.Empty;
        }

        if (_warningText != null)
        {
            _warningText.text = actor.IsDead ? "DEAD" : (isAlly ? string.Empty : (actor.GuardValue <= 0 ? "BROKEN" : string.Empty));
        }

        ApplyTurnVisual(isCurrentTurn);
    }

    // Backward-compatible wrappers.
    public void ApplyPlayerState(CombatActorRuntime player, bool isCurrentTurn, float guardBarMax)
    {
        ApplyActorState(player, isCurrentTurn, true);
    }

    public void ApplyEnemyState(CombatActorRuntime enemy, int maxEnemyGuard, int threatCap, bool isCurrentTurn)
    {
        ApplyActorState(enemy, isCurrentTurn, false);
    }

    public void PlayAttackLunge(float direction)
    {
        if (_motionRoot == null)
        {
            return;
        }

        _motionRoot.DOKill();
        _motionRoot.DOAnchorPos(_baseMotionPos + new Vector2(34f * direction, 0f), 0.08f)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (_motionRoot == null)
                {
                    return;
                }

                _motionRoot.DOAnchorPos(_baseMotionPos, 0.15f)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .SetEase(Ease.OutBack);
            });
    }

    public void PlayHitReaction()
    {
        if (_hitFlash != null)
        {
            _hitFlash.DOKill();
            _hitFlash.color = new Color(1f, 1f, 1f, 0f);
            _hitFlash.DOFade(0.5f, 0.05f)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (_hitFlash == null)
                    {
                        return;
                    }

                    _hitFlash.DOFade(0f, 0.12f).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                });
        }

        if (_motionRoot != null)
        {
            _motionRoot.DOKill();
            _motionRoot.DOShakeAnchorPos(0.18f, new Vector2(18f, 0f), 20, 90f, false, true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (_motionRoot != null)
                    {
                        _motionRoot.anchoredPosition = _baseMotionPos;
                    }
                });
        }
    }

    private void ApplyTurnVisual(bool isCurrentTurn)
    {
        if (_border != null)
        {
            _border.color = isCurrentTurn ? _turnHighlightColor : _normalBorderColor;
        }

        Color targetTextColor = isCurrentTurn ? _turnHighlightColor : _normalTextColor;
        if (_turnTintTexts != null)
        {
            for (int i = 0; i < _turnTintTexts.Length; i++)
            {
                if (_turnTintTexts[i] != null)
                {
                    _turnTintTexts[i].color = targetTextColor;
                }
            }
        }
    }

    private void CacheVisualDefaults()
    {
        if (_visualCached)
        {
            return;
        }

        _baseMotionPos = _motionRoot != null ? _motionRoot.anchoredPosition : Vector2.zero;
        if (_border != null)
        {
            _normalBorderColor = _border.color;
        }

        TMP_Text sample = _hpText;
        if (sample == null && _turnTintTexts != null && _turnTintTexts.Length > 0)
        {
            sample = _turnTintTexts[0];
        }

        if (sample != null)
        {
            _normalTextColor = sample.color;
        }

        _visualCached = true;
    }
}
