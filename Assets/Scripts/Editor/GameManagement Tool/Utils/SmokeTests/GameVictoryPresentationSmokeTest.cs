using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies victory timing, independent player control, committed progression and interruption cleanup in ECS.
/// </summary>
public static class GameVictoryPresentationSmokeTest
{
    #region Methods

    #region Entry Point
    /// <summary>
    /// Exercises both timing modes and both control policies without changing project presets.
    /// </summary>
    // [MenuItem("Tools/Game/HUD/Run Victory Presentation Smoke Test")]
    public static void Run()
    {
        float previousTimeScale = Time.timeScale;

        try
        {
            // Check every meaningful combination, including defeat and missing presentation.
            ValidateFreeze(false, false, true, PlayerRunOutcome.Victory);
            ValidateFreeze(false, true, true, PlayerRunOutcome.Victory);
            ValidateFreeze(true, false, true, PlayerRunOutcome.Victory);
            ValidateFreeze(true, true, true, PlayerRunOutcome.Victory);
            ValidateFreeze(true, false, false, PlayerRunOutcome.Victory);
            ValidateFreeze(true, false, true, PlayerRunOutcome.Defeat);
            ValidateBake();
            ValidateStandardFinalMessage();
            Debug.Log("[GameVictoryPresentationSmokeTest] All checks passed.");
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
    }
    #endregion

    #region Runtime Cases
    /// <summary>
    /// Checks the actual freeze system before and after the presentation releases its menu gate.
    /// </summary>
    /// <param name="delayTime">Whether the announcement delays the global freeze.</param>
    /// <param name="freezeInput">Whether control remains frozen during that delay.</param>
    /// <param name="hasPresentation">Whether a gated announcement singleton exists.</param>
    /// <param name="outcome">Committed victory or defeat being tested.</param>
    private static void ValidateFreeze(bool delayTime, bool freezeInput, bool hasPresentation, PlayerRunOutcome outcome)
    {
        using (World world = new World("VictoryFreezeSmokeTest"))
        {
            EntityManager manager = world.EntityManager;
            Entity player = manager.CreateEntity(typeof(PlayerControllerConfig), typeof(PlayerRunOutcomeState),
                                                  typeof(PlayerInputState), typeof(PlayerMovementState),
                                                  typeof(PlayerLookState), typeof(PlayerShootingState),
                                                  typeof(PlayerRunTimerConfig), typeof(PlayerRunTimerState));
            manager.SetComponentData(player, new PlayerRunOutcomeState { Outcome = outcome, IsFinalized = 1 });
            manager.SetComponentData(player, new PlayerInputState { Move = new float2(1f, 0f), Shoot = 1f });
            manager.SetComponentData(player, new PlayerMovementState { Velocity = new float3(1f, 0f, 0f) });
            manager.SetComponentData(player, new PlayerRunTimerState { CurrentSeconds = 23f });
            Entity presentationEntity = Entity.Null;

            if (hasPresentation)
            {
                presentationEntity = manager.CreateEntity(typeof(GameHudWaveClearAnnouncementRuntimeConfig),
                                                           typeof(GameHudWaveClearAnnouncementPresentationState));
                manager.SetComponentData(presentationEntity, new GameHudWaveClearAnnouncementRuntimeConfig
                {
                    Enabled = 1,
                    DelayVictoryTimeFreeze = delayTime ? (byte)1 : (byte)0,
                    FreezePlayerInputDuringVictoryDelay = freezeInput ? (byte)1 : (byte)0
                });
                manager.SetComponentData(presentationEntity,
                    new GameHudWaveClearAnnouncementPresentationState { Pending = 1, BlocksVictoryMenu = 1 });
            }

            // Preserve a reduced incoming time scale; a delay must not replace it with a hard-coded normal speed.
            Time.timeScale = 0.6f;
            SystemHandle freezeSystem = world.GetOrCreateSystem<PlayerRunOutcomeFreezeSystem>();
            freezeSystem.Update(world.Unmanaged);
            bool deferred = delayTime && hasPresentation && outcome == PlayerRunOutcome.Victory;
            bool liveInput = deferred && !freezeInput;
            PlayerRunOutcomeState result = manager.GetComponentData<PlayerRunOutcomeState>(player);
            Require(Mathf.Approximately(Time.timeScale, deferred ? 0.6f : 0f), "Incorrect victory/defeat time freeze.");
            Require(result.IsFinalized != 0 && result.Outcome == outcome, "Presentation changed the committed result.");
            Require((result.VictoryInputAllowed != 0) == liveInput, "Player control did not match the selected delay policy.");
            Require(PlayerRunOutcomeRuntimeUtility.IsInputFrozen(in result) != liveInput, "Input bridge gate disagrees with the freeze policy.");
            Require((manager.GetComponentData<PlayerInputState>(player).Shoot > 0f) == liveInput, "Input reset was applied in the wrong timing mode.");
            Require((math.lengthsq(manager.GetComponentData<PlayerMovementState>(player).Velocity) > 0f) == liveInput,
                    "Movement was reset in the wrong timing mode.");
            world.SetTime(new Unity.Core.TimeData(1d, 0.1f));
            world.GetOrCreateSystem<PlayerRunTimerSystem>().Update(world.Unmanaged);
            Require(manager.GetComponentData<PlayerRunTimerState>(player).CurrentSeconds == 23f,
                    "Live victory input continued the completed run timer.");

            // Completion and cancellation both release the same ECS gate and must freeze live input immediately.
            if (hasPresentation)
            {
                manager.SetComponentData(presentationEntity, new GameHudWaveClearAnnouncementPresentationState());
                freezeSystem.Update(world.Unmanaged);
                result = manager.GetComponentData<PlayerRunOutcomeState>(player);
                Require(Time.timeScale == 0f && result.VictoryInputAllowed == 0 && result.RuntimeFreezeApplied != 0,
                        "Announcement completion did not freeze the completed run.");
                Require(manager.GetComponentData<PlayerInputState>(player).Shoot == 0f,
                        "Held shooting survived announcement completion.");
            }

            // A new run must not retain a live-input grant from the prior victory.
            manager.SetComponentData(player, new PlayerRunOutcomeState());
            Time.timeScale = 1f;
            freezeSystem.Update(world.Unmanaged);
            Require(Time.timeScale == 1f && manager.GetComponentData<PlayerRunOutcomeState>(player).VictoryInputAllowed == 0,
                    "Reset retained the previous victory freeze policy.");
        }
    }

    /// <summary>
    /// Verifies the delay gate works with standard final-room text and releases for disabled or empty announcements.
    /// </summary>
    private static void ValidateStandardFinalMessage()
    {
        using (World world = new World("VictoryStandardMessageSmokeTest"))
        {
            EntityManager manager = world.EntityManager;
            Entity progress = manager.CreateEntity(typeof(GameRoomClearAnnouncementProgressState), typeof(GameRoomCombatCompletionState));
            Entity presentation = manager.CreateEntity(typeof(GameHudWaveClearAnnouncementRuntimeConfig),
                                                       typeof(GameHudWaveClearAnnouncementPresentationState));
            GameHudWaveClearAnnouncementRuntimeConfig config = new GameHudWaveClearAnnouncementRuntimeConfig
            {
                Enabled = 1,
                DelayVictoryTimeFreeze = 1,
                Content = new Unity.Collections.FixedString512Bytes("ROOM CLEARED")
            };
            manager.SetComponentData(presentation, config);
            SystemHandle requestSystem = world.GetOrCreateSystem<GameHudWaveClearAnnouncementRequestSystem>();
            requestSystem.Update(world.Unmanaged);
            manager.SetComponentData(progress, new GameRoomCombatCompletionState { IsComplete = 1 });
            requestSystem.Update(world.Unmanaged);
            Require(manager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentation).BlocksVictoryMenu != 0,
                    "Standard final content did not gate the delayed victory.");

            // Reset the observed edge, then make the announcement unavailable without adding a timed fallback.
            manager.SetComponentData(progress, new GameRoomCombatCompletionState());
            requestSystem.Update(world.Unmanaged);
            config.Enabled = 0;
            manager.SetComponentData(presentation, config);
            manager.SetComponentData(progress, new GameRoomCombatCompletionState { IsComplete = 1 });
            requestSystem.Update(world.Unmanaged);
            Require(manager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentation).BlocksVictoryMenu == 0,
                    "Disabled announcement retained the victory gate.");

            manager.SetComponentData(progress, new GameRoomCombatCompletionState());
            requestSystem.Update(world.Unmanaged);
            config.Enabled = 1;
            config.Content = default;
            manager.SetComponentData(presentation, config);
            manager.SetComponentData(progress, new GameRoomCombatCompletionState { IsComplete = 1 });
            requestSystem.Update(world.Unmanaged);
            Require(manager.GetComponentData<GameHudWaveClearAnnouncementPresentationState>(presentation).BlocksVictoryMenu == 0,
                    "Empty announcement retained the victory gate.");
        }
    }
    #endregion

    #region Bake Cases
    /// <summary>
    /// Verifies both booleans survive serialization, duplication and the shared ECS bake path.
    /// </summary>
    private static void ValidateBake()
    {
        GameHudManagerPreset preset = ScriptableObject.CreateInstance<GameHudManagerPreset>();
        GameHudManagerPreset clone = ScriptableObject.CreateInstance<GameHudManagerPreset>();

        try
        {
            SerializedObject serialized = new SerializedObject(preset);
            serialized.FindProperty("waveClearAnnouncementSettings.delayVictoryTimeFreeze").boolValue = true;
            serialized.FindProperty("waveClearAnnouncementSettings.freezePlayerInputDuringVictoryDelay").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset), clone);
            GameHudWaveClearAnnouncementRuntimeConfig config =
                GameHudSupplementalPresetBakeUtility.BuildWaveClearAnnouncementConfig(clone.WaveClearAnnouncementSettings);
            Require(config.DelayVictoryTimeFreeze != 0 && config.FreezePlayerInputDuringVictoryDelay == 0,
                    "Victory settings were lost during preset serialization or bake.");
            Require(GameHudCreditsRuntimeConfig.Build(clone).CloseActionId.ToString() == "UI/CreditsClose",
                    "New HUD presets have no dedicated Credits close action.");
            GameHudWaveClearAnnouncementSmokeTestUtility.ValidateRequestRuntime(config);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    /// <summary>
    /// Stops the smoke suite with the invariant that failed.
    /// </summary>
    /// <param name="condition">Invariant expected from the tested production systems.</param>
    /// <param name="message">Diagnostic describing the unexpected behavior.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
