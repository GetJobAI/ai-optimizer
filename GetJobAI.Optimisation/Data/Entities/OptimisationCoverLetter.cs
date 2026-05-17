namespace GetJobAI.Optimisation.Data.Entities;

public class OptimisationCoverLetter
{
    public Guid OptimisationId { get; private set; }
    
    public string CoverLetter { get; set; } = string.Empty;
    
    public int WordCount { get; set; }
    
    public string SalutationUsed { get; set; } = string.Empty;
    
    public List<string> KeyPointsMade { get; set; } = [];
    
    public bool? Accepted { get; set; }
    
    public int RewriteCount { get; set; }
    
    public DateTime GeneratedAt { get; private set; }

    public Optimisation Optimisation { get; private set; } = null!;

    private OptimisationCoverLetter() { }

    public static OptimisationCoverLetter Create(
        Guid optimisationId,
        string coverLetter,
        int wordCount,
        string salutationUsed,
        List<string> keyPointsMade) => new()
    {
        OptimisationId = optimisationId,
        CoverLetter = coverLetter,
        WordCount = wordCount,
        SalutationUsed = salutationUsed,
        KeyPointsMade = keyPointsMade,
        GeneratedAt = DateTime.UtcNow
    };

    public void Update(string coverLetter, int wordCount, string salutationUsed, List<string> keyPointsMade)
    {
        CoverLetter = coverLetter;
        WordCount = wordCount;
        SalutationUsed = salutationUsed;
        KeyPointsMade = keyPointsMade;
        Accepted = null;
        GeneratedAt = DateTime.UtcNow;
    }
}
