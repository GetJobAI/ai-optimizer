using System.Text.Json;
using GetJobAI.Optimisation.Contracts;
using GetJobAI.Optimisation.Data;
using GetJobAI.Optimisation.Messaging.Events;
using GetJobAI.Optimisation.Messaging.Events.ResumeScored;
using GetJobAI.Optimisation.OptimisationService.Contexts;
using GetJobAI.Optimisation.OptimisationService.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Entities = GetJobAI.Optimisation.Data.Entities;

namespace GetJobAI.Optimisation.Messaging.Consumers;

public class ResumeScoredConsumer(
    IOptimisationOrchestrator orchestrator,
    OptimisationDbContext db,
    ILogger<ResumeScoredConsumer> logger) : IConsumer<ResumeScoredEvent>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Consume(ConsumeContext<ResumeScoredEvent> context)
    {
        var msg = context.Message;
        var breakdown = msg.Breakdown;

        var resume = await db.Resumes
            .Include(r => r.WorkExperiences)
            .Include(r => r.Skills)
            .Include(r => r.Publications)
            .Include(r => r.Activities)
            .Include(r => r.AdditionalSections)
            .FirstOrDefaultAsync(r => r.Id == msg.ResumeId, context.CancellationToken);

        if (resume is null)
        {
            logger.LogWarning(
                "Resume {ResumeId} not found in local snapshot — skipping optimisation for job {JobAnalysisId}",
                msg.ResumeId, msg.JobAnalysisId);
            
            return;
        }

        var optimisation = Entities.Optimisation.Create(
            resumeId: msg.ResumeId,
            jobAnalysisId: msg.JobAnalysisId,
            jobTitle: msg.JobTitle,
            companyName: msg.CompanyName,
            overallScore: msg.Score,
            scoreKeywordEarned: (short)breakdown.KeywordMatchRate.Earned,
            scoreKeywordMax: (short)breakdown.KeywordMatchRate.Max,
            scoreSkillEarned: (short)breakdown.SkillAlignment.Earned,
            scoreSkillMax: (short)breakdown.SkillAlignment.Max,
            scoreFormatEarned: (short)breakdown.FormatAndParseability.Earned,
            scoreFormatMax: (short)breakdown.FormatAndParseability.Max,
            scoreExperienceEarned: (short)breakdown.ExperienceRelevance.Earned,
            scoreExperienceMax: (short)breakdown.ExperienceRelevance.Max,
            atsDetailsJson: JsonSerializer.Serialize(breakdown, JsonOptions));

        db.Optimisations.Add(optimisation);
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Optimisation {OptimisationId} started — resume {ResumeId}, job {JobAnalysisId}, score {Score}",
            optimisation.Id, msg.ResumeId, msg.JobAnalysisId, msg.Score);

        optimisation.Start();
        await db.SaveChangesAsync(context.CancellationToken);

        try
        {
            var optimisationContext = BuildContext(optimisation, msg, resume);
            var suggestions = await orchestrator.RunAsync(optimisationContext, context.CancellationToken);

            SaveSuggestions(optimisation.Id, suggestions);
            optimisation.Complete(suggestions.AtsExplanation, suggestions.SkillsGap);
            await db.SaveChangesAsync(context.CancellationToken);

            await context.Publish(new ResumeOptimized
            {
                OptimisationId = optimisation.Id,
                ResumeId = optimisation.ResumeId,
                OriginalAtsScore = optimisation.OverallScore,
                Status = "AwaitingReview"
            });

            logger.LogInformation("Optimisation {OptimisationId} completed", optimisation.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Optimisation {OptimisationId} failed", optimisation.Id);

            optimisation.Fail(ex.Message);
            await db.SaveChangesAsync(context.CancellationToken);

            await context.Publish(new ResumeOptimized
            {
                OptimisationId = optimisation.Id,
                ResumeId = optimisation.ResumeId,
                OriginalAtsScore = optimisation.OverallScore,
                Status = "Failed",
                ErrorMessage = ex.Message
            });
        }
    }

    private void SaveSuggestions(Guid optimisationId, AiSuggestionsDocument doc)
    {
        if (doc.Summary is not null)
        {
            db.SummarySuggestions.Add(Entities.OptimisationSummarySuggestion.Create(
                optimisationId,
                doc.Summary.Original,
                doc.Summary.Rewritten,
                doc.Summary.KeywordsIncorporated));
        }

        foreach (var we in doc.WorkExperience)
        {
            var weEntity = Entities.OptimisationWorkExperienceSuggestion.Create(
                optimisationId, we.EntryId, we.Include, we.Reason);

            foreach (var bullet in we.Bullets)
                db.BulletSuggestions.Add(Entities.OptimisationBulletSuggestion.Create(
                    weEntity.Id,
                    bullet.Original,
                    bullet.Rewritten,
                    bullet.KeywordsAdded,
                    bullet.XyzApplied));

            db.WorkExperienceSuggestions.Add(weEntity);
        }

        foreach (var activity in doc.Activities)
            db.ActivitySuggestions.Add(Entities.OptimisationActivitySuggestion.Create(
                optimisationId,
                activity.EntryId,
                activity.Include,
                activity.Reason,
                activity.HighlightsRewritten));

        foreach (var pub in doc.Publications)
            db.SectionSuggestions.Add(Entities.OptimisationSectionSuggestion.Create(
                optimisationId,
                Entities.OptimisationSectionCategory.Publication,
                pub.EntryId,
                pub.SectionType,
                pub.Include,
                pub.Reason));

        foreach (var section in doc.AdditionalSections)
            db.SectionSuggestions.Add(Entities.OptimisationSectionSuggestion.Create(
                optimisationId,
                Entities.OptimisationSectionCategory.AdditionalSection,
                section.EntryId,
                section.SectionType,
                section.Include,
                section.Reason));
    }

    private static OptimisationContext BuildContext(
        Entities.Optimisation optimisation,
        ResumeScoredEvent msg,
        Entities.Resume resume)
    {
        var bd = msg.Breakdown;

        return new OptimisationContext
        {
            OptimisationId = optimisation.Id,
            ResumeId = optimisation.ResumeId,
            JobTitle = msg.JobTitle,
            CompanyName = msg.CompanyName,
            CandidateName = resume.CandidateName,
            ExistingSummary = resume.ExistingSummary,
            DetectedLanguage = resume.DetectedLanguage,
            OverallScore = optimisation.OverallScore,
            ScoreKeywordEarned = optimisation.ScoreKeywordEarned,
            ScoreKeywordMax = optimisation.ScoreKeywordMax,
            ScoreSkillEarned = optimisation.ScoreSkillEarned,
            ScoreSkillMax = optimisation.ScoreSkillMax,
            ScoreFormatEarned = optimisation.ScoreFormatEarned,
            ScoreFormatMax = optimisation.ScoreFormatMax,
            ScoreExperienceEarned = optimisation.ScoreExperienceEarned,
            ScoreExperienceMax = optimisation.ScoreExperienceMax,

            MatchedKeywords = bd.KeywordMatchRate.Details?.Match ?? [],
            PartialKeywords = bd.KeywordMatchRate.Details?.Partial ?? [],
            MissingKeywords = bd.KeywordMatchRate.Details?.Missing ?? [],

            SkillAlignmentDetails = (bd.SkillAlignment.Details ?? [])
                .Select(d => new SkillAlignmentContext
                {
                    RequiredSkill = d.RequiredSkill,
                    ClosestMatch = d.ClosestMatch,
                    VectorSimilarityScore = d.VectorSimilarityScore,
                    Flag = d.Flag
                })
                .ToList(),

            ExperienceGapDetails = (bd.ExperienceRelevance.Details ?? [])
                .Select(d => new ExperienceGapContext
                {
                    JobResponsibility = d.JobResponsibility,
                    ClosestMatch = d.ClosestMatch,
                    VectorSimilarityScore = d.VectorSimilarityScore,
                    Flag = d.Flag
                })
                .ToList(),

            ParsingFlags = new AtsParsingFlagsContext
            {
                HasComplexLayout = bd.FormatAndParseability.ParsingFlags.HasComplexLayout,
                HasGraphics = bd.FormatAndParseability.ParsingFlags.HasGraphics,
                HasHeadersFooters = bd.FormatAndParseability.ParsingFlags.HasHeadersFooters,
                HasNonStandardFonts = bd.FormatAndParseability.ParsingFlags.HasNonStandardFonts
            },

            WorkExperiences = resume.WorkExperiences
                .Select(we => new WorkExperienceContext
                {
                    EntryId = we.Id,
                    JobTitle = we.JobTitle,
                    CompanyName = we.CompanyName,
                    StartDate = we.StartDate,
                    EndDate = we.EndDate,
                    Bullets = we.Bullets
                })
                .ToList(),

            Skills = resume.Skills
                .Select(s => new SkillContext
                {
                    SkillName = s.SkillName,
                    SkillNameRaw = s.SkillNameRaw,
                    Proficiency = s.Proficiency,
                    Category = s.Category
                })
                .ToList(),

            Publications = resume.Publications
                .Select(p => new PublicationContext
                {
                    EntryId = p.Id,
                    Title = p.Title,
                    Publisher = p.Publisher,
                    PublicationDate = p.PublicationDate,
                    Description = p.Description
                })
                .ToList(),

            Activities = resume.Activities
                .Select(a => new ActivityContext
                {
                    EntryId = a.Id,
                    ActivityName = a.ActivityName,
                    Organization = a.Organization,
                    Role = a.Role,
                    Highlights = a.Highlights
                })
                .ToList(),

            AdditionalSections = resume.AdditionalSections
                .Select(s => new AdditionalSectionContext
                {
                    EntryId = s.Id,
                    SectionType = s.SectionType ?? string.Empty,
                    Title = s.Title ?? string.Empty,
                    ContentJson = s.ContentJson ?? string.Empty
                })
                .ToList(),

            JobRequiredSkills = (bd.SkillAlignment.Details ?? [])
                .Select(d => new JobSkillContext
                {
                    SkillName = d.RequiredSkill,
                    ImportanceScore = d.VectorSimilarityScore,
                    IsRequired = true
                })
                .ToList(),

            JobPreferredSkills = []
        };
    }
}
