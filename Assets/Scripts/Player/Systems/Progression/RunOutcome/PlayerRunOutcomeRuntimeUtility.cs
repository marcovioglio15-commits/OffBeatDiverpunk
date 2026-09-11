using Unity.Entities;

/// <summary>
/// Provides shared helpers used by runtime systems that must react to finalized player-run outcomes.
/// None.
/// </summary>
public static class PlayerRunOutcomeRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Separates committed progression from the optional live-control window before the victory panel.
    /// </summary>
    /// <param name="state">Authoritative player outcome and current victory input policy.</param>
    /// <returns>True when the run outcome must suppress player control and follow-camera motion.</returns>
    public static bool IsInputFrozen(in PlayerRunOutcomeState state)
    {
        // Dying never allows input, and live victory control requires an explicit runtime grant.
        return state.IsDying != 0 ||
               state.IsFinalized != 0 &&
               (state.Outcome != PlayerRunOutcome.Victory || state.VictoryInputAllowed == 0);
    }

    /// <summary>
    /// Resolves the per-player control policy without changing progression or telemetry finalization.
    /// </summary>
    /// <param name="entity">Player whose input is being sampled.</param>
    /// <param name="runOutcomeLookup">Read-only outcome lookup for the current system update.</param>
    /// <returns>True when the player has an outcome that freezes its input.</returns>
    public static bool IsInputFrozen(Entity entity, in ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup)
    {
        if (!runOutcomeLookup.HasComponent(entity))
            return false;

        return IsInputFrozen(runOutcomeLookup[entity]);
    }

    /// <summary>
    /// Returns whether the requested entity owns a finalized run outcome.
    /// Used by gameplay systems that must stop consuming live state after victory or defeat.
    /// </summary>
    /// <param name="entity">Entity whose run-outcome state should be inspected.</param>
    /// <param name="runOutcomeLookup">Component lookup used to read PlayerRunOutcomeState.</param>
    /// <returns>True when the entity has a finalized run outcome, otherwise false.</returns>
    public static bool IsFinalized(Entity entity, in ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup)
    {
        if (!runOutcomeLookup.HasComponent(entity))
            return false;

        return runOutcomeLookup[entity].IsFinalized != 0;
    }
    #endregion

    #endregion
}
