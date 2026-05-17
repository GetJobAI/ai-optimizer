namespace GetJobAI.Optimisation.Api.Responses;

/// <summary>An AI-rewritten resume bullet point.</summary>
/// <param name="Id">Bullet ID.</param>
/// <param name="Original">Original bullet text from the resume.</param>
/// <param name="Rewritten">AI-rewritten bullet text.</param>
/// <param name="KeywordsAdded">Job-description keywords injected to improve ATS match.</param>
/// <param name="XyzApplied">Whether the XYZ structure (Accomplished X by doing Y, resulting in Z) was applied.</param>
/// <param name="Accepted"><c>true</c> accepted, <c>false</c> rejected, <c>null</c> not yet reviewed.</param>
public record BulletSuggestionResponse(
    Guid Id,
    string Original,
    string Rewritten,
    List<string> KeywordsAdded,
    bool XyzApplied,
    bool? Accepted);
