using System;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Verifies authored Credits navigation, keyboard and gamepad closing, action rebinding and focus restoration.
/// </summary>
[InitializeOnLoad]
public static class GameCreditsPlayModeSmokeTest
{
    #region Constants
    private const string activeKey = "OffBeat.CreditsSmoke.Active";
    private const string enteredKey = "OffBeat.CreditsSmoke.Entered";
    private const string failureKey = "OffBeat.CreditsSmoke.Failure";
    private const string startKey = "OffBeat.CreditsSmoke.Start";
    #endregion

    #region Fields
    private static MainMenuController menu;
    private static CreditsMenuController credits;
    private static Button creditsButton;
    private static Button playButton;
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static InputAction closeAction;
    private static InputSettings previousInputSettings;
    private static InputSettings testInputSettings;
    private static string previousBindingOverrides;
    private static bool previousRunInBackground;
    private static int phase;
    private static int phaseFrame;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Reconnects the smoke workflow after entering or leaving Play Mode reloads assemblies.
    /// </summary>
    static GameCreditsPlayModeSmokeTest()
    {
        // Keep the editor update loop untouched outside an explicitly started smoke run.
        if (SessionState.GetBool(activeKey, false))
            RegisterCallbacks();
    }
    #endregion

    #region Entry Point
    /// <summary>
    /// Loads the production bootstrap and validates the resulting main menu in Play Mode.
    /// </summary>
    // [MenuItem("Tools/Game/HUD/Run Credits Play Mode Smoke Test")]
    public static void Run()
    {
        SessionState.SetBool(activeKey, true);
        SessionState.SetBool(enteredKey, false);
        SessionState.SetString(failureKey, string.Empty);
        SessionState.SetString(startKey, DateTime.UtcNow.Ticks.ToString());
        RegisterCallbacks();
        GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Connects one set of callbacks for the active run, including runs resumed after an assembly reload.
    /// </summary>
    private static void RegisterCallbacks()
    {
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    /// <summary>
    /// Records Play Mode entry and restores temporary input before the runtime world is released.
    /// </summary>
    /// <param name="state">Editor Play Mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                SessionState.SetBool(enteredKey, true);
                break;
            case PlayModeStateChange.ExitingPlayMode:
                CleanupInput();
                break;
        }
    }

    /// <summary>
    /// Advances input checks on distinct game frames and reports completion after leaving Play Mode.
    /// </summary>
    private static void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(enteredKey, false))
                Finish();

            return;
        }

        try
        {
            long startTicks = long.Parse(SessionState.GetString(startKey, "0"));

            if (TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTicks).TotalSeconds > 120d)
                throw new InvalidOperationException("Credits Play Mode validation timed out.");

            if (Time.frameCount <= phaseFrame + 2)
                return;

            Advance();
        }
        catch (Exception exception)
        {
            SessionState.SetString(failureKey, exception.ToString());
            CleanupInput();
            EditorApplication.isPlaying = false;
        }
    }
    #endregion

    #region Input Cases
    /// <summary>
    /// Exercises the authored button callbacks with actual Input System events instead of invoking close directly.
    /// </summary>
    private static void Advance()
    {
        switch (phase)
        {
            case 0:
                if (!TryResolveMenu())
                    return;

                // Batch mode has no focused Game view; isolate input settings so synthetic device events reach runtime.
                previousInputSettings = InputSystem.settings;
                testInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
                testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings = testInputSettings;
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                previousBindingOverrides = closeAction.SaveBindingOverridesAsJson();
                keyboard = InputSystem.AddDevice<Keyboard>();
                gamepad = InputSystem.AddDevice<Gamepad>();
                GameMenuPreviewCaptureUtility.Capture(menu.GetComponentInParent<Canvas>(), "../TaskArtifacts/OffBeat/main-menu.png");
                break;
            case 1:
                OpenCredits();
                GameMenuPreviewCaptureUtility.Capture(menu.GetComponentInParent<Canvas>(), "../TaskArtifacts/OffBeat/credits.png");
                break;
            case 2:
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                break;
            case 3:
                VerifyClosed();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                OpenCredits();
                break;
            case 4:
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
                break;
            case 5:
                VerifyClosed();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                closeAction.ApplyBindingOverride(0, "<Keyboard>/f2");
                OpenCredits();
                break;
            case 6:
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                break;
            case 7:
                Require(credits.gameObject.activeSelf, "Credits ignored its rebound close key.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
                break;
            case 8:
                VerifyClosed();
                EditorApplication.isPlaying = false;
                return;
        }

        phase++;
        phaseFrame = Time.frameCount;
    }

    /// <summary>
    /// Finds the loaded authored menu once the scene transition has finished and verifies its persistent wiring.
    /// </summary>
    /// <returns>True when the production menu is ready for interaction.</returns>
    private static bool TryResolveMenu()
    {
        menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();

        World world = World.DefaultGameObjectInjectionWorld;

        if (menu == null || world == null || !world.IsCreated)
            return false;

        using (EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneTransitionState>()))
            if (query.CalculateEntityCount() != 1 || query.GetSingleton<GameSceneTransitionState>().IsTransitioning != 0)
                return false;

        SerializedObject serialized = new SerializedObject(menu);
        creditsButton = serialized.FindProperty("creditsButton").objectReferenceValue as Button;
        playButton = serialized.FindProperty("playButton").objectReferenceValue as Button;
        credits = serialized.FindProperty("creditsMenu").objectReferenceValue as CreditsMenuController;
        Require(creditsButton != null && playButton != null && credits != null, "Credits scene references are missing.");
        Require(!credits.gameObject.activeSelf, "Credits must start closed.");
        Require(creditsButton.GetComponent<MenuSelectableHoverRelay>() != null &&
                creditsButton.GetComponent<MenuSelectableAudioRelay>() != null, "Credits lacks shared button feedback.");
        closeAction = PlayerInputRuntime.ResolveRuntimeAction("UI/CreditsClose", "UI/CreditsClose");

        if (closeAction == null)
        {
            SerializedObject panel = new SerializedObject(credits);
            InputActionAsset asset = panel.FindProperty("inputActions").objectReferenceValue as InputActionAsset;
            closeAction = asset != null ? asset.FindAction("UI/CreditsClose", false) : null;
        }

        Require(closeAction != null, "Dedicated Credits action is missing.");
        return true;
    }

    /// <summary>
    /// Opens Credits through its authored button and checks modal menu ownership.
    /// </summary>
    private static void OpenCredits()
    {
        EventSystem.current.SetSelectedGameObject(creditsButton.gameObject);
        creditsButton.onClick.Invoke();
        Require(credits.gameObject.activeSelf && !playButton.interactable && !creditsButton.interactable,
                "Credits did not take exclusive main-menu navigation ownership.");
    }

    /// <summary>
    /// Checks that action-based closure restored both commands and focus to the Credits button.
    /// </summary>
    private static void VerifyClosed()
    {
        Require(!credits.gameObject.activeSelf && playButton.interactable && creditsButton.interactable,
                "Credits did not close or restore menu commands at phase " + phase + ". Action=" + closeAction.enabled +
                ", Panel=" + credits.gameObject.activeSelf + ", Play=" + playButton.interactable + ".");
        Require(EventSystem.current.currentSelectedGameObject == creditsButton.gameObject,
                "Credits closure did not restore the opening button's focus.");
    }
    #endregion

    #region Completion
    /// <summary>
    /// Removes only the temporary test bindings and devices before leaving the runtime world.
    /// </summary>
    private static void CleanupInput()
    {
        if (closeAction != null)
        {
            closeAction.RemoveAllBindingOverrides();

            if (!string.IsNullOrEmpty(previousBindingOverrides))
                closeAction.LoadBindingOverridesFromJson(previousBindingOverrides);
        }

        if (keyboard != null)
            InputSystem.RemoveDevice(keyboard);

        if (gamepad != null)
            InputSystem.RemoveDevice(gamepad);

        if (previousInputSettings != null)
        {
            InputSystem.settings = previousInputSettings;
            Application.runInBackground = previousRunInBackground;
        }

        if (testInputSettings != null)
            UnityEngine.Object.DestroyImmediate(testInputSettings);
    }

    /// <summary>
    /// Reports the persisted result and exits only a batch test process.
    /// </summary>
    private static void Finish()
    {
        string failure = SessionState.GetString(failureKey, string.Empty);
        SessionState.SetBool(activeKey, false);
        SessionState.SetBool(enteredKey, false);
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;

        if (string.IsNullOrEmpty(failure))
            Debug.Log("[GameCreditsPlayModeSmokeTest] Keyboard, gamepad, rebinding and focus checks passed.");
        else
            Debug.LogError("[GameCreditsPlayModeSmokeTest] " + failure);

        if (Application.isBatchMode)
            EditorApplication.Exit(string.IsNullOrEmpty(failure) ? 0 : 1);
    }

    /// <summary>
    /// Reports the first broken UI invariant with a useful failure message.
    /// </summary>
    /// <param name="condition">Expected runtime invariant.</param>
    /// <param name="message">Failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
