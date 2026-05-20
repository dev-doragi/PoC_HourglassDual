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

    private CombatActorType _teamType;
    private int _slotIndex;
    private Action<CombatActorType, int> _onSelect;
    private MaterialPropertyBlock _propertyBlock;
    private bool _clickable;

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
        SetSelected(false);
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<PrimaryActionInputEvent>(OnPrimaryActionInput);
    }

    public void Bind(CombatActorType teamType, int slotIndex, Action<CombatActorType, int> onSelect)
    {
        _teamType = teamType;
        _slotIndex = Mathf.Max(0, slotIndex);
        _onSelect = onSelect;
    }

    public void ApplyActor(CombatActorRuntime actor, bool selected)
    {
        if (actor == null)
        {
            SetBars(0, 1, 0, 1);
            SetGroggy(false, string.Empty);
            SetSelected(false);
            SetClickable(false);
            return;
        }

        int hpMax = Mathf.Max(1, actor.MaxHp);
        int guardMax = Mathf.Max(1, actor.MaxGuard);
        SetBars(actor.CurrentHp, hpMax, actor.GuardValue, guardMax);
        bool showGroggy = actor.BreakSkipCount > 0 || actor.IsBroken;
        SetGroggy(showGroggy, showGroggy ? "GROGGY" : string.Empty);

        bool selectable = !actor.IsDead;
        SetClickable(selectable);
        SetSelected(selectable && selected);
    }

    public void SetSelected(bool selected)
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(OutlineEnabledId, selected ? 1f : 0f);
        _propertyBlock.SetColor(OutlineColorId, _selectedOutlineColor);
        _propertyBlock.SetFloat(OutlineSizeId, selected ? Mathf.Max(0f, _selectedOutlineSize) : 0f);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void OnClickSelect()
    {
        _onSelect?.Invoke(_teamType, _slotIndex);
    }

    private void OnPrimaryActionInput(PrimaryActionInputEvent evt)
    {
        if (!evt.IsPressed || !_clickable || _clickCollider == null || !evt.HasScreenPosition)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(evt.ScreenPosition);
        Vector2 point = new Vector2(world.x, world.y);
        if (_clickCollider.OverlapPoint(point))
        {
            OnClickSelect();
        }
    }

    private void SetBars(int hp, int hpMax, int guard, int guardMax)
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
