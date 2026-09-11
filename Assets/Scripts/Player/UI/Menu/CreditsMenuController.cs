using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Opens the authored empty Credits panel and returns menu ownership through its dedicated input action.
/// </summary>
[DisallowMultipleComponent]
public sealed class CreditsMenuController : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Input")]
    [Tooltip("Shared action asset containing the Credits close action selected in the HUD preset.")]
    [SerializeField]
    private InputActionAsset inputActions;
    #endregion

    #region Runtime
    private InputAction closeAction;
    private bool closeActionEnabledByPanel;
    private bool isOpen;
    private int openedFrame;
    #endregion

    #endregion

    #region Events
    /// <summary>
    /// Releases main-menu navigation after the panel is closed or disabled.
    /// </summary>
    public event Action MenuClosed;
    #endregion

    #region Methods

    #region Panel Lifecycle
    /// <summary>
    /// Resolves input once per opening and shows the existing panel only when it has a usable close action.
    /// </summary>
    /// <returns>True when Credits owns the visible panel and its close callback.</returns>
    public bool Open()
    {
        // Repeated clicks must not duplicate subscriptions or overwrite action ownership.
        if (isOpen)
            return true;

        closeAction = ResolveCloseAction();

        if (closeAction == null || closeAction.type != InputActionType.Button || closeAction.bindings.Count == 0)
        {
            Debug.LogWarning("[CreditsMenuController] Assign a bound Button action in HUD > Credits > Close Action.", this);
            closeAction = null;
            return false;
        }

        // The scene owns the UI hierarchy; opening only changes visibility and input ownership.
        isOpen = true;
        openedFrame = Time.frameCount;
        closeActionEnabledByPanel = !closeAction.enabled;
        closeAction.performed += HandleClosePerformed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (closeActionEnabledByPanel)
            closeAction.Enable();

        return true;
    }

    /// <summary>
    /// Hides the panel and releases its owned action before returning focus to the main menu.
    /// </summary>
    public void Close()
    {
        // OnDisable performs the shared release path for explicit and external closure.
        if (isOpen)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Removes callbacks on closure, scene unloading, or component disable without changing shared action state.
    /// </summary>
    private void OnDisable()
    {
        if (!isOpen)
            return;

        isOpen = false;

        // Disable only an action enabled by this panel; another UI owner may already have enabled it.
        if (closeAction != null)
        {
            closeAction.performed -= HandleClosePerformed;

            if (closeActionEnabledByPanel)
                closeAction.Disable();
        }

        closeAction = null;
        closeActionEnabledByPanel = false;
        MenuClosed?.Invoke();
    }
    #endregion

    #region Input
    /// <summary>
    /// Closes Credits on a fresh press without consuming the same input event that opened it.
    /// </summary>
    /// <param name="context">Performed callback from the configured close action.</param>
    private void HandleClosePerformed(InputAction.CallbackContext context)
    {
        if (isOpen && Time.frameCount != openedFrame)
            Close();
    }

    /// <summary>
    /// Reads the baked selection once and resolves it against runtime input or the authored project asset.
    /// </summary>
    /// <returns>Configured action, or the dedicated project default when its selection cannot be resolved.</returns>
    private InputAction ResolveCloseAction()
    {
        string actionId = "UI/CreditsClose";
        World world = World.DefaultGameObjectInjectionWorld;

        // Query only during opening, then release the query before subscribing to input.
        if (world != null && world.IsCreated)
        {
            using (EntityQuery query = world.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<GameHudCreditsRuntimeConfig>()))
                if (query.CalculateEntityCount() == 1)
                    actionId = query.GetSingleton<GameHudCreditsRuntimeConfig>().CloseActionId.ToString();
        }

        InputAction action = PlayerInputRuntime.ResolveRuntimeAction(actionId, "UI/CreditsClose");

        if (action != null || inputActions == null)
            return action;

        return inputActions.FindAction(actionId, false) ?? inputActions.FindAction("UI/CreditsClose", false);
    }
    #endregion

    #endregion
}
