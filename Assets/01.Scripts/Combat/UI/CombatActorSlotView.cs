using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatActorSlotView : MonoBehaviour
{
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineSizeId = Shader.PropertyToID("_OutlineSize");

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Collider2D _clickCollider;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private Slider _guardBar;
    [SerializeField] private TMP_Text _groggyText;

    [Header("Outline")]
    [SerializeField] private Color _selectedOutlineColor = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] private float _selectedOutlineSize = 1.5f;
    [SerializeField] private Color _targetableOutlineColor = new Color(1f, 0.9f, 0.35f, 1f);
    [SerializeField] private float _targetableOutlineSize = 1.05f;
    [SerializeField] private Color _targetHoverOutlineColor = new Color(1f, 0.95f, 0.2f, 1f);
    [SerializeField] private float _targetHoverOutlineSize = 1.9f;
    [SerializeField] private Color _targetLockedOutlineColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float _targetLockedOutlineSize = 2.2f;

    private CombatActorType _teamType;
    private int _slotIndex;
    private Action<CombatActorType, int> _onSelect;
    private Action<CombatActorType, int, bool> _onHoverChanged;
    private MaterialPropertyBlock _propertyBlock;
    private bool _clickable;
    private bool _selected;
    private bool _targetingMode;
    private bool _targetable;
    private bool _targetHover;
    private bool _targetLocked;
    private bool _pointerHovering;

    private void Awake()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_clickCollider == null && _spriteRenderer != null)
        {
            _clickCollider = _spriteRenderer.GetComponent<Collider2D>();
        }

        _propertyBlock = new MaterialPropertyBlock();
        ApplyOutlineVisual();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
        EventBus.Instance.Subscribe<PointerPositionInputEvent>(OnPointerPositionInput);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
        EventBus.Instance.Unsubscribe<PointerPositionInputEvent>(OnPointerPositionInput);
        if (_pointerHovering)
        {
            _pointerHovering = false;
            _onHoverChanged?.Invoke(_teamType, _slotIndex, false);
        }
    }

    public void Bind(
        CombatActorType teamType,
        int slotIndex,
        Action<CombatActorType, int> onSelect,
        Action<CombatActorType, int, bool> onHoverChanged)
    {
        _teamType = teamType;
        _slotIndex = Mathf.Max(0, slotIndex);
        _onSelect = onSelect;
        _onHoverChanged = onHoverChanged;
    }

    public void ApplyActor(CombatActorRuntime actor, bool selected)
    {
        if (actor == null)
        {
            SetBars(0, 1, 0, 1, true);
            SetGroggy(false, string.Empty);
            SetSelected(false);
            SetClickable(false);
            return;
        }

        int hpMax = Mathf.Max(1, actor.MaxHp);
        int guardMax = Mathf.Max(1, actor.MaxGuard);
        bool showGuard = true;
        SetBars(actor.CurrentHp, hpMax, actor.GuardValue, guardMax, showGuard);
        bool showGroggy = actor.BreakSkipCount > 0 || actor.IsBroken;
        SetGroggy(showGroggy, showGroggy ? "GROGGY" : string.Empty);

        bool selectable = !actor.IsDead;
        SetClickable(selectable);
        SetSelected(selectable && selected);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyOutlineVisual();
    }

    public void SetTargetingMode(bool enabled)
    {
        _targetingMode = enabled;
        if (!enabled)
        {
            _targetHover = false;
            _targetLocked = false;
            _targetable = false;
            if (_pointerHovering)
            {
                _pointerHovering = false;
                _onHoverChanged?.Invoke(_teamType, _slotIndex, false);
            }
        }

        ApplyOutlineVisual();
    }

    public void SetTargetable(bool targetable)
    {
        _targetable = targetable;
        ApplyOutlineVisual();
    }

    public void SetTargetHover(bool hover)
    {
        _targetHover = hover;
        ApplyOutlineVisual();
    }

    public void SetTargetLocked(bool locked)
    {
        _targetLocked = locked;
        ApplyOutlineVisual();
    }

    public void ClearTargetHighlight()
    {
        _targetable = false;
        _targetHover = false;
        _targetLocked = false;
        ApplyOutlineVisual();
    }

    private void ApplyOutlineVisual()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        bool enable = false;
        Color color = _selectedOutlineColor;
        float size = 0f;

        if (_targetLocked)
        {
            enable = true;
            color = _targetLockedOutlineColor;
            size = Mathf.Max(0f, _targetLockedOutlineSize);
        }
        else if (_targetHover)
        {
            enable = true;
            color = _targetHoverOutlineColor;
            size = Mathf.Max(0f, _targetHoverOutlineSize);
        }
        else if (_selected)
        {
            enable = true;
            color = _selectedOutlineColor;
            size = Mathf.Max(0f, _selectedOutlineSize);
        }
        else if (_targetable)
        {
            enable = true;
            color = _targetableOutlineColor;
            size = Mathf.Max(0f, _targetableOutlineSize);
        }

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(OutlineEnabledId, enable ? 1f : 0f);
        _propertyBlock.SetColor(OutlineColorId, color);
        _propertyBlock.SetFloat(OutlineSizeId, enable ? size : 0f);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void OnClickSelect()
    {
        _onSelect?.Invoke(_teamType, _slotIndex);
    }

    private void OnPrimaryActionInput(PrimaryActionInputEvent evt)
    {
        if (!evt.IsPressed || !_clickable || !evt.HasScreenPosition)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (IsPointerOnSlot(evt.ScreenPosition))
        {
            OnClickSelect();
        }
    }

    private void OnPointerPositionInput(PointerPositionInputEvent evt)
    {
        if (!_targetingMode || _teamType != CombatActorType.Enemy || !_clickable || !evt.HasScreenPosition)
        {
            if (_pointerHovering)
            {
                _pointerHovering = false;
                _onHoverChanged?.Invoke(_teamType, _slotIndex, false);
            }

            return;
        }

        bool hover = IsPointerOnSlot(evt.ScreenPosition);
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            hover = false;
        }

        if (hover == _pointerHovering)
        {
            return;
        }

        _pointerHovering = hover;
        _onHoverChanged?.Invoke(_teamType, _slotIndex, hover);
    }

    private bool IsPointerOnSlot(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector3 world = cam.ScreenToWorldPoint(screenPosition);
        Vector2 point = new Vector2(world.x, world.y);

        if (_clickCollider != null && _clickCollider.enabled && _clickCollider.OverlapPoint(point))
        {
            return true;
        }

        if (_spriteRenderer == null)
        {
            return false;
        }

        Bounds bounds = _spriteRenderer.bounds;
        Vector3 worldPoint = new Vector3(point.x, point.y, bounds.center.z);
        return bounds.Contains(worldPoint);
    }

    private void SetBars(int hp, int hpMax, int guard, int guardMax, bool showGuard)
    {
        if (_hpBar != null)
        {
            _hpBar.minValue = 0;
            _hpBar.maxValue = Mathf.Max(1, hpMax);
            _hpBar.value = Mathf.Clamp(hp, 0, hpMax);
            _hpBar.interactable = false;
        }

        if (_guardBar != null)
        {
            _guardBar.gameObject.SetActive(showGuard);
            _guardBar.minValue = 0;
            _guardBar.maxValue = Mathf.Max(1, guardMax);
            _guardBar.value = Mathf.Clamp(guard, 0, guardMax);
            _guardBar.interactable = false;
        }
    }

    private void SetGroggy(bool active, string label)
    {
        if (_groggyText == null)
        {
            return;
        }

        _groggyText.gameObject.SetActive(active);
        _groggyText.text = label;
    }

    private void SetClickable(bool clickable)
    {
        _clickable = clickable;
        if (_clickCollider != null)
        {
            _clickCollider.enabled = clickable;
        }
    }
}
