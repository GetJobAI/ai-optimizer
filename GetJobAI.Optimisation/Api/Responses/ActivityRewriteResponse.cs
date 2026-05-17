namespace GetJobAI.Optimisation.Api.Responses;

/// <summary>Result of an AI rewrite of an activity entry.</summary>
/// <param name="Id">Suggestion ID.</param>
/// <param name="EntryId">Original resume entry ID.</param>
/// <param name="Include">Whether the AI recommends including this activity on the resume.</param>
/// <param name="Reason">AI explanation for the recommendation.</param>
/// <param name="HighlightsRewritten">Rewritten highlight bullets for the activity.</param>
/// <param name="RewriteCount">Total number of rewrites performed on this suggestion.</param>
public record ActivityRewriteResponse(
    Guid Id,
    Guid EntryId,
    bool Include,
    string? Reason,
    List<string> HighlightsRewritten,
    int RewriteCount);
