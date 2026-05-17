namespace GetJobAI.Optimisation.Api.Requests;

/// <summary>Accept or reject a suggestion.</summary>
/// <param name="Accepted"><c>true</c> to accept, <c>false</c> to reject.</param>
/// <param name="Hint">Optional rejection hint passed to the AI on the next rewrite. Ignored when <paramref name="Accepted"/> is <c>true</c>.</param>
public record ReviewRequest(bool Accepted, string? Hint = null);
