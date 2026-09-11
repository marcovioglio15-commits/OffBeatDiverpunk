using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Exposes Credits input and its existing main-menu button profile from the HUD preset tool.
/// </summary>
internal static class GameHudCreditsPanelUtility
{
    #region Methods

    #region Editor UI
    /// <summary>
    /// Builds the dedicated close-action selector without replacing invalid authored selections.
    /// </summary>
    /// <param name="root">HUD tab receiving the Credits controls.</param>
    /// <param name="serializedPreset">Current HUD preset draft.</param>
    public static void Build(VisualElement root, SerializedObject serializedPreset)
    {
        InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            PlayerInputActionsAssetUtility.DefaultInputAssetPath);
        SerializedProperty actionProperty = serializedPreset.FindProperty("creditsCloseActionId");
        Foldout input = GameHudManagerSupplementalPanelUtility.CreateFoldout(
            "Close Action", "Closes Credits and returns focus to its main-menu button.");
        InputActionSelectionElement selector = new InputActionSelectionElement(
            inputAsset, serializedPreset, actionProperty, InputActionSelectionElement.SelectionMode.UIButton);
        input.Add(selector);
        root.Add(input);
        VisualElement warningsRoot = new VisualElement();
        root.Add(warningsRoot);

        // Refresh diagnostics only after editing, while the shared draft session owns apply and discard.
        selector.ActionChanged += () =>
        {
            GameManagementDraftSession.MarkDirty();
            RefreshWarnings(warningsRoot, inputAsset, actionProperty.stringValue);
        };
        RefreshWarnings(warningsRoot, inputAsset, actionProperty.stringValue);
        Label profileNote = new Label("Button appearance: Menu Buttons > Main Menu > CreditsButton.");
        profileNote.tooltip = "Credits shares the main-menu motion, audio and text profile, with its own state-image entry.";
        root.Add(profileNote);
    }

    /// <summary>
    /// Reports missing, unbound or non-button input without altering its ID or bindings.
    /// </summary>
    /// <param name="root">Container reserved for current action warnings.</param>
    /// <param name="asset">Shared input asset searched by the selector.</param>
    /// <param name="actionId">Stored action ID or map/action path.</param>
    private static void RefreshWarnings(VisualElement root, InputActionAsset asset, string actionId)
    {
        root.Clear();
        InputAction action = asset != null && !string.IsNullOrWhiteSpace(actionId)
            ? asset.FindAction(actionId, false)
            : null;

        if (action == null || action.type != InputActionType.Button || action.bindings.Count == 0)
            root.Add(new HelpBox("Select a bound Button action to close Credits.", HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}
