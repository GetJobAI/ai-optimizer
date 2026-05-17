namespace GetJobAI.Optimisation.Api.Responses;

/// <summary>Result of an AI rewrite of a work experience entry.</summary>
/// <param name="Id">Suggestion ID.</param>
/// <param name="EntryId">Original resume entry ID.</param>
/// <param name="Include">Whether the AI recommends including this entry on the resume.</param>
/// <param name="Reason">AI explanation for the recommendation.</param>
/// <param name="RewriteCount">Total number of rewrites performed on this suggestion.</param>
/// <param name="Bullets">Rewritten bullet points for this entry.</param>
public record WorkExperienceRewriteResponse(
    Guid Id,
    Guid EntryId,
    bool Include,
    string? Reason,
    int RewriteCount,
    List<BulletSuggestionResponse> Bullets);
