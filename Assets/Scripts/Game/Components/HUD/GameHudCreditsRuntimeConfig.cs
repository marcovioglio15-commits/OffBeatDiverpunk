using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Stores the HUD-selected close action for the authored main-menu Credits panel.
/// </summary>
public struct GameHudCreditsRuntimeConfig : IComponentData
{
    #region Fields
    [Tooltip("Stable action ID or map/action path used while Credits owns menu input.")]
    public FixedString64Bytes CloseActionId;
    #endregion

    #region Methods

    #region Bake
    /// <summary>
    /// Builds the same Credits input configuration for baking and scene-manager runtime initialization.
    /// </summary>
    /// <param name="preset">HUD preset selected by the Game Master, or null for the project default.</param>
    /// <returns>Compact Credits input config with a safe default for missing or oversized action IDs.</returns>
    public static GameHudCreditsRuntimeConfig Build(GameHudManagerPreset preset)
    {
        // Validate storage capacity without rewriting the authored setting.
        string actionId = preset != null ? preset.CreditsCloseActionId : null;

        if (string.IsNullOrWhiteSpace(actionId) ||
            System.Text.Encoding.UTF8.GetByteCount(actionId) > FixedString64Bytes.UTF8MaxLengthInBytes)
            actionId = "UI/CreditsClose";

        return new GameHudCreditsRuntimeConfig { CloseActionId = new FixedString64Bytes(actionId) };
    }
    #endregion

    #endregion
}
