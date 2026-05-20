using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-78)]
[RequireComponent(typeof(PlayerInput))]
public class InputReader : Singleton<InputReader>
{
    [Header("Action Maps")]
    [SerializeField] private string _playerActionMapName = "Player";
    [SerializeField] private string _uiActionMapName = "UI";
    [SerializeField] private string _systemActionMapName = "System";
    [SerializeField] private string _combatActionMapName = "Combat";

    [Header("Player Actions")]
    [SerializeField] private string _moveActionName = "Move";
    [SerializeField] private string _lookActionName = "Look";
    [SerializeField] private string _primaryActionName = "PrimaryAttack";
    [SerializeField] private string _secondaryActionName = "SecondaryAttack";

    [Header("UI/System Actions")]
    [SerializeField] private string _submitActionName = "Submit";
    [SerializeField] private string _cancelActionName = "Cancel";
    [SerializeField] private string _pauseActionName = "Pause";

    [Header("Combat Actions")]
    [SerializeField] private string _strikeActionName = "Strike";
    [SerializeField] private string _pierceActionName = "Pierce";
    [SerializeField] private string _hexActionName = "Hex";
    [SerializeField] private string _guardActionName = "Guard";
    [SerializeField] private string _endTurnActionName = "EndTurn";

    private PlayerInput _playerInput;
    private InputActionMap _playerMap;
    private InputActionMap _uiMap;
    private InputActionMap _systemMap;
    private InputActionMap _combatMap;

    private InputAction _move;
    private InputAction _look;
    private InputAction _primary;
    private InputAction _secondary;
    private InputAction _submit;
    private InputAction _cancel;
    private InputAction _pause;
    private InputAction _strike;
    private InputAction _pierce;
    private InputAction _hex;
    private InputAction _guard;
    private InputAction _endTurn;

    protected override void OnBootstrap()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null || _playerInput.actions == null)
        {
            Debug.LogError("[InputReader] PlayerInput or InputActionAsset is missing.", this);
            return;
        }

        _playerMap = _playerInput.actions.FindActionMap(_playerActionMapName, false);
        _uiMap = _playerInput.actions.FindActionMap(_uiActionMapName, false);
        _systemMap = _playerInput.actions.FindActionMap(_systemActionMapName, false);
        _combatMap = _playerInput.actions.FindActionMap(_combatActionMapName, false);

        if (_playerMap == null || _uiMap == null || _systemMap == null || _combatMap == null)
        {
            Debug.LogError("[InputReader] Required action map is missing. Expected: Player/UI/System/Combat", this);
            return;
        }

        _move = RequireAction(_playerMap, _moveActionName);
        _look = RequireAction(_playerMap, _lookActionName);
        _primary = RequireAction(_playerMap, _primaryActionName);
        _secondary = RequireAction(_playerMap, _secondaryActionName);
        _submit = RequireAction(_uiMap, _submitActionName);
        _cancel = RequireAction(_uiMap, _cancelActionName);
        _pause = RequireAction(_systemMap, _pauseActionName);
        _strike = RequireAction(_combatMap, _strikeActionName);
        _pierce = RequireAction(_combatMap, _pierceActionName);
        _hex = RequireAction(_combatMap, _hexActionName);
        _guard = RequireAction(_combatMap, _guardActionName);
        _endTurn = RequireAction(_combatMap, _endTurnActionName);
        if (_move == null || _look == null || _primary == null || _secondary == null || _submit == null || _cancel == null || _pause == null || _strike == null || _pierce == null || _hex == null || _guard == null || _endTurn == null)
        {
            return;
        }

        Bind();
        _playerMap.Enable();
        _uiMap.Enable();
        _systemMap.Enable();
        _combatMap.Enable();

        _playerInput.onControlsChanged += OnControlsChanged;
    }

    private void OnDisable()
    {
        Unbind();

        if (_playerInput != null)
        {
            _playerInput.onControlsChanged -= OnControlsChanged;
        }

        _playerMap?.Disable();
        _uiMap?.Disable();
        _systemMap?.Disable();
        _combatMap?.Disable();
    }

    private InputAction RequireAction(InputActionMap map, string actionName)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogError($"[InputReader] Missing required action '{actionName}' in map '{map.name}'.", this);
        }

        return action;
    }

    private void Bind()
    {
        _move.performed += OnMovePerformed;
        _move.canceled += OnMoveCanceled;
        _look.performed += OnLookPerformed;
        _look.canceled += OnLookCanceled;

        _primary.started += OnPrimaryStarted;
        _primary.canceled += OnPrimaryCanceled;
        _secondary.started += OnSecondaryStarted;
        _secondary.canceled += OnSecondaryCanceled;

        _submit.performed += OnSubmit;
        _cancel.performed += OnCancel;
        _pause.performed += OnPause;
        _strike.performed += OnStrike;
        _pierce.performed += OnPierce;
        _hex.performed += OnHex;
        _guard.performed += OnGuard;
        _endTurn.performed += OnEndTurn;
    }

    private void Unbind()
    {
        if (_move != null)
        {
            _move.performed -= OnMovePerformed;
            _move.canceled -= OnMoveCanceled;
        }

        if (_look != null)
        {
            _look.performed -= OnLookPerformed;
            _look.canceled -= OnLookCanceled;
        }

        if (_primary != null)
        {
            _primary.started -= OnPrimaryStarted;
            _primary.canceled -= OnPrimaryCanceled;
        }

        if (_secondary != null)
        {
            _secondary.started -= OnSecondaryStarted;
            _secondary.canceled -= OnSecondaryCanceled;
        }

        if (_submit != null) _submit.performed -= OnSubmit;
        if (_cancel != null) _cancel.performed -= OnCancel;
        if (_pause != null) _pause.performed -= OnPause;
        if (_strike != null) _strike.performed -= OnStrike;
        if (_pierce != null) _pierce.performed -= OnPierce;
        if (_hex != null) _hex.performed -= OnHex;
        if (_guard != null) _guard.performed -= OnGuard;
        if (_endTurn != null) _endTurn.performed -= OnEndTurn;
    }

    private static void OnControlsChanged(PlayerInput input)
    {
        string deviceName = input.currentControlScheme;
        EventBus.Instance.Publish(new InputDeviceChangedEvent { DeviceName = deviceName });
    }

    private static void OnMovePerformed(InputAction.CallbackContext ctx) => EventBus.Instance.Publish(new MoveInputEvent { Value = ctx.ReadValue<Vector2>() });
    private static void OnMoveCanceled(InputAction.CallbackContext ctx) => EventBus.Instance.Publish(new MoveInputEvent { Value = Vector2.zero });
    private static void OnLookPerformed(InputAction.CallbackContext ctx) => EventBus.Instance.Publish(new LookInputEvent { Value = ctx.ReadValue<Vector2>() });
    private static void OnLookCanceled(InputAction.CallbackContext ctx) => EventBus.Instance.Publish(new LookInputEvent { Value = Vector2.zero });
    private static void OnPrimaryStarted(InputAction.CallbackContext _)
    {
        Vector2 screenPosition = Vector2.zero;
        bool hasScreenPosition = false;

        if (Pointer.current != null)
        {
            screenPosition = Pointer.current.position.ReadValue();
            hasScreenPosition = true;
        }

        EventBus.Instance.Publish(new PrimaryActionInputEvent
        {
            IsPressed = true,
            HasScreenPosition = hasScreenPosition,
            ScreenPosition = screenPosition
        });
    }

    private static void OnPrimaryCanceled(InputAction.CallbackContext _)
    {
        EventBus.Instance.Publish(new PrimaryActionInputEvent
        {
            IsPressed = false,
            HasScreenPosition = false,
            ScreenPosition = Vector2.zero
        });
    }
    private static void OnSecondaryStarted(InputAction.CallbackContext _) => EventBus.Instance.Publish(new SecondaryActionInputEvent { IsPressed = true });
    private static void OnSecondaryCanceled(InputAction.CallbackContext _) => EventBus.Instance.Publish(new SecondaryActionInputEvent { IsPressed = false });
    private static void OnSubmit(InputAction.CallbackContext _) => EventBus.Instance.Publish(new SubmitInputEvent());
    private static void OnCancel(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CancelInputEvent());
    private static void OnPause(InputAction.CallbackContext _) => EventBus.Instance.Publish(new PauseInputEvent());
    private static void OnStrike(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CombatStrikeInputEvent());
    private static void OnPierce(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CombatPierceInputEvent());
    private static void OnHex(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CombatHexInputEvent());
    private static void OnGuard(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CombatGuardInputEvent());
    private static void OnEndTurn(InputAction.CallbackContext _) => EventBus.Instance.Publish(new CombatEndTurnInputEvent());
}
