using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static PlayerGameplayMenuSetupSharedUtility;

/// <summary>
/// Maintains the authored Credits prefab, main-menu button and navigation during editor menu rebuilds.
/// </summary>
internal static class PlayerCreditsMenuSetupUtility
{
    #region Constants
    private const string prefabPath = "Assets/Prefabs/UI/PF_CreditsMenu.prefab";
    #endregion

    #region Methods

    #region Scene Setup
    /// <summary>
    /// Adds missing Credits assets and wires the existing main menu without replacing authored button styling.
    /// </summary>
    /// <param name="controller">Main-menu controller in the scene being rebuilt.</param>
    /// <param name="canvas">Canvas owning the menu and its modal panels.</param>
    public static void EnsureMenu(MainMenuController controller, Canvas canvas)
    {
        if (controller == null || canvas == null)
            throw new InvalidOperationException("Credits setup requires a main-menu controller and canvas.");

        SerializedObject serializedController = new SerializedObject(controller);
        Button settingsButton = serializedController.FindProperty("settingsButton").objectReferenceValue as Button;
        Button creditsButton = serializedController.FindProperty("creditsButton").objectReferenceValue as Button;

        // Clone an authored sibling only when rebuilding a menu that does not yet contain Credits.
        if (creditsButton == null)
        {
            if (settingsButton == null)
                throw new InvalidOperationException("Credits setup requires the authored Settings button.");

            creditsButton = CreateButton(settingsButton);
            serializedController.FindProperty("creditsButton").objectReferenceValue = creditsButton;
        }

        CreditsMenuController creditsMenu = serializedController.FindProperty("creditsMenu").objectReferenceValue as CreditsMenuController;

        if (creditsMenu == null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(EnsurePrefab(), canvas.transform) as GameObject;

            if (instance == null)
                throw new InvalidOperationException("Unable to instantiate the authored Credits prefab.");

            creditsMenu = instance.GetComponent<CreditsMenuController>();
            StretchToParent((RectTransform)instance.transform);
            instance.transform.SetAsLastSibling();
            serializedController.FindProperty("creditsMenu").objectReferenceValue = creditsMenu;
        }

        // Store the closed panel and the complete cyclic graph in the scene itself.
        creditsMenu.gameObject.SetActive(false);
        MenuVerticalNavigationUtility.ConfigureCyclic(
            serializedController.FindProperty("playButton").objectReferenceValue as Button,
            settingsButton,
            creditsButton,
            serializedController.FindProperty("enemySpawnerToolButton").objectReferenceValue as Button,
            serializedController.FindProperty("quitButton").objectReferenceValue as Button);
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #region Authored Assets
    /// <summary>
    /// Returns the reusable closed Credits prefab, creating its empty modal surface only when absent.
    /// </summary>
    /// <returns>Persistent prefab asset used by the main-menu scene.</returns>
    private static GameObject EnsurePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab != null)
            return prefab;

        GameObject root = new GameObject("PF_CreditsMenu", typeof(RectTransform), typeof(Image), typeof(CreditsMenuController));

        try
        {
            // An opaque, raycast-blocking surface keeps the authored menu covered while Credits owns input.
            root.layer = LayerMask.NameToLayer("UI");
            StretchToParent((RectTransform)root.transform);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.055f, 1f);
            background.raycastTarget = true;
            SerializedObject serializedPanel = new SerializedObject(root.GetComponent<CreditsMenuController>());
            serializedPanel.FindProperty("inputActions").objectReferenceValue = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Copies layout and feedback components, then gives Credits its own label and image-content identity.
    /// </summary>
    /// <param name="template">Authored Settings button supplying the visual hierarchy.</param>
    /// <returns>Credits button inserted immediately after Settings.</returns>
    private static Button CreateButton(Button template)
    {
        GameObject root = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
        root.name = "CreditsButton";
        root.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        Button button = root.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.text = "Credits";
            label.enabled = true;
            label.gameObject.SetActive(true);
        }

        // The existing relay supports both authored text and a future per-button image mapping.
        MenuSelectableHoverRelay relay = GetOrAddComponent<MenuSelectableHoverRelay>(root);
        GetOrAddComponent<MenuSelectableAudioRelay>(root);
        SerializedObject serializedRelay = new SerializedObject(relay);
        serializedRelay.FindProperty("buttonContentId").stringValue = root.name;
        serializedRelay.FindProperty("menuKind").enumValueIndex = (int)GameUiMenuKind.MainMenu;
        Image imageContent = serializedRelay.FindProperty("targetImageOverride").objectReferenceValue as Image;

        if (imageContent != null)
        {
            imageContent.sprite = null;
            imageContent.enabled = false;
        }

        serializedRelay.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }
    #endregion

    #endregion
}
