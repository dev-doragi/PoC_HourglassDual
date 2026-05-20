using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatActorSlotView : MonoBehaviour
{
    [SerializeField] private Button _selectButton;
    [SerializeField] private Image _selectionBorder;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private TMP_Text _guardText;
    [SerializeField] private TMP_Text _intentText;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private Slider _guardBar;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color _normalColor = Color.white;

    private CombatActorType _teamType;
    private int _slotIndex;
    private Action<CombatActorType, int> _onSelect;

    private void Awake()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(OnClickSelect);
        }
    }

    public void Bind(CombatActorType teamType, int slotIndex, Action<CombatActorType, int> onSelect)
    {
        _teamType = teamType;
        _slotIndex = Mathf.Max(0, slotIndex);
        _onSelect = onSelect;
    }

    public void ApplyActor(CombatActorRuntime actor, bool selected, CombatIntentRuntime? intent, bool intentAffordable)
    {
        if (_selectionBorder != null)
        {
            _selectionBorder.color = selected ? _selectedColor : _normalColor;
        }

        if (actor == null)
        {
            SetTexts("-", "HP -", "G -");
            SetIntent("-");
            SetBars(0, 1, 0, 1);
            return;
        }

        SetTexts(actor.DisplayName, $"HP {actor.CurrentHp}/{Mathf.Max(1, actor.MaxHp)}", $"G {actor.GuardValue}/{Mathf.Max(0, actor.MaxGuard)}");
        SetBars(actor.CurrentHp, Mathf.Max(1, actor.MaxHp), actor.GuardValue, Mathf.Max(1, actor.MaxGuard));

        if (intent.HasValue)
        {
            CombatIntentRuntime intentValue = intent.Value;
            string skipMark = actor.BreakSkipCount > 0 ? " X" : string.Empty;
            string affordability = intentAffordable ? "Ready" : "NotEnoughSand";
            SetIntent($"{intentValue.DisplayName} c{intentValue.EffectiveCost} {affordability}{skipMark}");
        }
        else
        {
            SetIntent(actor.BreakSkipCount > 0 ? "Skip X" : "-");
        }
    }

    private void OnClickSelect()
    {
        _onSelect?.Invoke(_teamType, _slotIndex);
    }

    private void SetTexts(string name, string hp, string guard)
    {
        if (_nameText != null) _nameText.text = name;
        if (_hpText != null) _hpText.text = hp;
        if (_guardText != null) _guardText.text = guard;
    }

    private void SetIntent(string value)
    {
        if (_intentText != null)
        {
            _intentText.text = value;
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
}
