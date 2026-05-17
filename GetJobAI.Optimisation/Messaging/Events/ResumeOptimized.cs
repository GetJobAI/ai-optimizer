using MassTransit;

namespace GetJobAI.Optimisation.Messaging.Events;

[EntityName("resume.optimized")]
public record ResumeOptimized
{
    public Guid OptimisationId { get; init; }

    public Guid ResumeId { get; init; }
    
    public Guid UserId { get; init; }

    public int OriginalAtsScore { get; init; }

    public string Status { get; init; } = "AwaitingReview";
    
    public string? ErrorMessage { get; init; }
}