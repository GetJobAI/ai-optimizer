namespace GetJobAI.Optimisation.Api.Requests;

/// <summary>Options for generating a cover letter.</summary>
/// <param name="CompanyDescription">A brief description of the company, used to personalise the letter.</param>
/// <param name="TopAchievements">
///   Specific achievements to highlight. When omitted, the top 5 rewritten work-experience bullets
///   from the optimisation session are used automatically.
/// </param>
/// <param name="CustomNote">Free-text instruction for the AI (tone, specific points to mention, language).</param>
public record GenerateCoverLetterRequest(
    string? CompanyDescription = null,
    List<string>? TopAchievements = null,
    string? CustomNote = null);
