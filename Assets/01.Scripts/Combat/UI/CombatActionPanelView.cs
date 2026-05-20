using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombatActionPanelView : MonoBehaviour
{
    [Header("Q Basic")]
    [SerializeField] private Button _basicButton;
    [SerializeField] private TMP_Text _basicNameText;
    [SerializeField] private TMP_Text _basicCostText;
    [SerializeField] private TMP_Text _basicEffectText;
    [SerializeField] private TMP_Text _basicKeyText;

    [Header("W Special")]
    [SerializeField] private Button _specialButton;
    [SerializeField] private TMP_Text _specialNameText;
    [SerializeField] private TMP_Text _specialCostText;
    [SerializeField] private TMP_Text _specialEffectText;
    [SerializeField] private TMP_Text _specialKeyText;

    [Header("E Guard")]
    [SerializeField] private Button _guardButton;
    [SerializeField] private TMP_Text _guardNameText;
    [SerializeField] private TMP_Text _guardCostText;
    [SerializeField] private TMP_Text _guardEffectText;
    [SerializeField] private TMP_Text _guardKeyText;

    [Header("R Flip")]
    [SerializeField] private Button _flipButton;
    [SerializeField] private TMP_Text _flipNameText;
    [SerializeField] private TMP_Text _flipCostText;
    [SerializeField] private TMP_Text _flipEffectText;
    [SerializeField] private TMP_Text _flipKeyText;

    public Button BasicButton => _basicButton;
    public Button SpecialButton => _specialButton;
    public Button GuardButton => _guardButton;
    public Button FlipButton => _flipButton;

    private void Awake()
    {
        RegisterClearSelection(_basicButton);
        RegisterClearSelection(_specialButton);
        RegisterClearSelection(_guardButton);
        RegisterClearSelection(_flipButton);
    }

    private void OnDestroy()
    {
        UnregisterClearSelection(_basicButton);
        UnregisterClearSelection(_specialButton);
        UnregisterClearSelection(_guardButton);
        UnregisterClearSelection(_flipButton);
    }

    public void SetStaticTexts()
    {
        SetTexts(_basicNameText, _basicCostText, _basicEffectText, "-", "-", "-");
        SetTexts(_specialNameText, _specialCostText, _specialEffectText, "-", "-", "-");
        SetTexts(_guardNameText, _guardCostText, _guardEffectText, "-", "-", "-");
        SetTexts(_flipNameText, _flipCostText, _flipEffectText, "확정", "R", "명령 확정/실행");

        SetText(_basicKeyText, "Q");
        SetText(_specialKeyText, "W");
        SetText(_guardKeyText, "E");
        SetText(_flipKeyText, "R");

    }

    public void SetActionSlot(int index, string actionName, int cost, string effect, bool interactable, bool active = true)
    {
        TMP_Text nameText = null;
        TMP_Text costText = null;
        TMP_Text effectText = null;
        Button button = null;

        if (index == 0)
        {
            nameText = _basicNameText;
            costText = _basicCostText;
            effectText = _basicEffectText;
            button = _basicButton;
        }
        else if (index == 1)
        {
            nameText = _specialNameText;
            costText = _specialCostText;
            effectText = _specialEffectText;
            button = _specialButton;
        }
        else if (index == 2)
        {
            nameText = _guardNameText;
            costText = _guardCostText;
            effectText = _guardEffectText;
            button = _guardButton;
        }

        if (button != null)
        {
            button.gameObject.SetActive(active);
            button.interactable = active && interactable;
        }

        if (nameText != null) nameText.text = active ? actionName : string.Empty;
        if (costText != null) costText.text = active ? $"Cost {Mathf.Max(0, cost)}" : string.Empty;
        if (effectText != null) effectText.text = active ? effect : string.Empty;
    }

    public void SetFlipInteractable(bool interactable)
    {
        SetButtonState(_flipButton, interactable);
    }

    public void SetEndTurnPreview(bool _, int nextSand)
    {
        if (_flipEffectText != null)
        {
            _flipEffectText.text = $"Pred EnemySand: {Mathf.Max(0, nextSand)}";
        }
    }

    public void SetAllInteractable(bool basic, bool special, bool guard, bool flip)
    {
        SetButtonState(_basicButton, basic);
        SetButtonState(_specialButton, special);
        SetButtonState(_guardButton, guard);
        SetButtonState(_flipButton, flip);
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

