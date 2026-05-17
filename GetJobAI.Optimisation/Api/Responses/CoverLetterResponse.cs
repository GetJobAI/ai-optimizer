namespace GetJobAI.Optimisation.Api.Responses;

/// <summary>An AI-generated cover letter.</summary>
/// <param name="CoverLetter">Full cover letter text.</param>
/// <param name="WordCount">Word count of the generated letter.</param>
/// <param name="SalutationUsed">Salutation chosen by the AI (e.g. "Dear Hiring Manager").</param>
/// <param name="KeyPointsMade">Key achievements the AI highlighted in the letter.</param>
/// <param name="Accepted"><c>true</c> accepted, <c>false</c> rejected, <c>null</c> not yet reviewed.</param>
/// <param name="RewriteCount">Total number of times this cover letter has been generated.</param>
/// <param name="GeneratedAt">UTC timestamp of the most recent generation.</param>
public record CoverLetterResponse(
    string CoverLetter,
    int WordCount,
    string SalutationUsed,
    List<string> KeyPointsMade,
    bool? Accepted,
    int RewriteCount,
    DateTime GeneratedAt);
