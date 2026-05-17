namespace GetJobAI.Optimisation.Api.Requests;

/// <summary>Options for an AI rewrite.</summary>
/// <param name="Hint">Optional guidance passed to the AI (e.g. "focus on leadership impact").</param>
public record RewriteRequest(string? Hint = null);
