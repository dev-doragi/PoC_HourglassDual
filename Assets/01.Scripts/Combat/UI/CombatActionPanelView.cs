using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatActionPanelView : MonoBehaviour
{
    [Header("Q")]
    [SerializeField] private Button _strikeButton;
    [SerializeField] private TMP_Text _strikeNameText;
    [SerializeField] private TMP_Text _strikeCostText;
    [SerializeField] private TMP_Text _strikeEffectText;
    [SerializeField] private TMP_Text _strikeKeyText;

    [Header("W")]
    [SerializeField] private Button _pierceButton;
    [SerializeField] private TMP_Text _pierceNameText;
    [SerializeField] private TMP_Text _pierceCostText;
    [SerializeField] private TMP_Text _pierceEffectText;
    [SerializeField] private TMP_Text _pierceKeyText;

    [Header("E")]
    [SerializeField] private Button _hexButton;
    [SerializeField] private TMP_Text _hexNameText;
    [SerializeField] private TMP_Text _hexCostText;
    [SerializeField] private TMP_Text _hexEffectText;
    [SerializeField] private TMP_Text _hexKeyText;

    [Header("R")]
    [SerializeField] private Button _guardButton;
    [SerializeField] private TMP_Text _guardNameText;
    [SerializeField] private TMP_Text _guardCostText;
    [SerializeField] private TMP_Text _guardEffectText;
    [SerializeField] private TMP_Text _guardKeyText;

    [Header("F")]
    [SerializeField] private Button _endTurnButton;
    [SerializeField] private TMP_Text _endTurnNameText;
    [SerializeField] private TMP_Text _endTurnCostText;
    [SerializeField] private TMP_Text _endTurnEffectText;
    [SerializeField] private TMP_Text _endTurnKeyText;

    public Button StrikeButton => _strikeButton;
    public Button PierceButton => _pierceButton;
    public Button HexButton => _hexButton;
    public Button GuardButton => _guardButton;
    public Button EndTurnButton => _endTurnButton;

    private void Awake()
    {
        RegisterClearSelection(_strikeButton);
        RegisterClearSelection(_pierceButton);
        RegisterClearSelection(_hexButton);
        RegisterClearSelection(_guardButton);
        RegisterClearSelection(_endTurnButton);
    }

    private void OnDestroy()
    {
        UnregisterClearSelection(_strikeButton);
        UnregisterClearSelection(_pierceButton);
        UnregisterClearSelection(_hexButton);
        UnregisterClearSelection(_guardButton);
        UnregisterClearSelection(_endTurnButton);
    }

    public void SetStaticTexts()
    {
        SetTexts(_strikeNameText, _strikeCostText, _strikeEffectText, "Q Action", "-", "-");
        SetTexts(_pierceNameText, _pierceCostText, _pierceEffectText, "W Action", "-", "-");
        SetTexts(_hexNameText, _hexCostText, _hexEffectText, "E Action", "-", "-");
        SetTexts(_guardNameText, _guardCostText, _guardEffectText, "Target Next", "No Cost", "R: 적 타겟 순환");
        SetTexts(_endTurnNameText, _endTurnCostText, _endTurnEffectText, "Confirm", "F", "명령 확정/해결");

        SetText(_strikeKeyText, "Q");
        SetText(_pierceKeyText, "W");
        SetText(_hexKeyText, "E");
        SetText(_guardKeyText, "R");
        SetText(_endTurnKeyText, "F");
    }

    public void SetActionSlot(int index, string actionName, int cost, int speed, string effect, bool interactable)
    {
        TMP_Text nameText = null;
        TMP_Text costText = null;
        TMP_Text effectText = null;
        Button button = null;

        if (index == 0)
        {
            nameText = _strikeNameText;
            costText = _strikeCostText;
            effectText = _strikeEffectText;
            button = _strikeButton;
        }
        else if (index == 1)
        {
            nameText = _pierceNameText;
            costText = _pierceCostText;
            effectText = _pierceEffectText;
            button = _pierceButton;
        }
        else if (index == 2)
        {
            nameText = _hexNameText;
            costText = _hexCostText;
            effectText = _hexEffectText;
            button = _hexButton;
        }

        if (nameText != null) nameText.text = actionName;
        if (costText != null) costText.text = $"Cost {Mathf.Max(0, cost)} / Spd {Mathf.Max(0, speed)}";
        if (effectText != null) effectText.text = effect;
        if (button != null) button.interactable = interactable;
    }

    public void SetEndTurnPreview(bool nextIsEnemy, int nextSand)
    {
        if (_endTurnEffectText != null)
        {
            _endTurnEffectText.text = $"Pred EnemySand: {Mathf.Max(0, nextSand)}";
        }
    }

    public void SetInteractable(bool strike, bool pierce, bool hex, bool guard, bool endTurn)
    {
        SetButtonState(_strikeButton, strike);
        SetButtonState(_pierceButton, pierce);
        SetButtonState(_hexButton, hex);
        SetButtonState(_guardButton, guard);
        SetButtonState(_endTurnButton, endTurn);
    }

    private static void SetTexts(TMP_Text name, TMP_Text cost, TMP_Text effect, string nameValue, string costValue, string effectValue)
    {
        if (name != null) name.text = nameValue;
        if (cost != null) cost.text = costValue;
        if (effect != null) effect.text = effectValue;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    private static void SetButtonState(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void RegisterClearSelection(Button button)
    {
        if (button != null)
        {
            button.onClick.AddListener(ClearSelection);
        }
    }

    private static void UnregisterClearSelection(Button button)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ClearSelection);
        }
    }

    private static void ClearSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
