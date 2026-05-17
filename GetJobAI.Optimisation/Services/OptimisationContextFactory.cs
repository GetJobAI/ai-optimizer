using System.Text.Json;
using GetJobAI.Optimisation.Data;
using GetJobAI.Optimisation.Messaging.Events.ResumeScored;
using GetJobAI.Optimisation.OptimisationService.Contexts;
using Microsoft.EntityFrameworkCore;
using Entities = GetJobAI.Optimisation.Data.Entities;

namespace GetJobAI.Optimisation.Services;

public class OptimisationContextFactory(OptimisationDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OptimisationContext?> CreateAsync(Guid optimisationId, CancellationToken ct)
    {
        var optimisation = await db.Optimisations
            .FirstOrDefaultAsync(o => o.Id == optimisationId, ct);
        if (optimisation is null) return null;

        var resume = await db.Resumes
            .Include(r => r.WorkExperiences)
            .Include(r => r.Skills)
            .Include(r => r.Publications)
            .Include(r => r.Activities)
            .Include(r => r.AdditionalSections)
            .FirstOrDefaultAsync(r => r.Id == optimisation.ResumeId, ct);
        if (resume is null) return null;

        var breakdown = optimisation.AtsDetailsJson is not null
            ? JsonSerializer.Deserialize<AtsBreakdown>(optimisation.AtsDetailsJson, JsonOptions) ?? new AtsBreakdown()
            : new AtsBreakdown();

        return Build(optimisation, resume, breakdown);
    }

    private static OptimisationContext Build(
        Entities.Optimisation optimisation,
        Entities.Resume resume,
        AtsBreakdown bd) => new()
    {
        OptimisationId = optimisation.Id,
        ResumeId = optimisation.ResumeId,
        JobTitle = optimisation.JobTitle,
        CompanyName = optimisation.CompanyName,
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
            }).ToList(),
        ExperienceGapDetails = (bd.ExperienceRelevance.Details ?? [])
            .Select(d => new ExperienceGapContext
            {
                JobResponsibility = d.JobResponsibility,
                ClosestMatch = d.ClosestMatch,
                VectorSimilarityScore = d.VectorSimilarityScore,
                Flag = d.Flag
            }).ToList(),
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
            }).ToList(),
        Skills = resume.Skills
            .Select(s => new SkillContext
            {
                SkillName = s.SkillName,
                SkillNameRaw = s.SkillNameRaw,
                Proficiency = s.Proficiency,
                Category = s.Category
            }).ToList(),
        Publications = resume.Publications
            .Select(p => new PublicationContext
            {
                EntryId = p.Id,
                Title = p.Title,
                Publisher = p.Publisher,
                PublicationDate = p.PublicationDate,
                Description = p.Description
            }).ToList(),
        Activities = resume.Activities
            .Select(a => new ActivityContext
            {
                EntryId = a.Id,
                ActivityName = a.ActivityName,
                Organization = a.Organization,
                Role = a.Role,
                Highlights = a.Highlights
            }).ToList(),
        AdditionalSections = resume.AdditionalSections
            .Select(s => new AdditionalSectionContext
            {
                EntryId = s.Id,
                SectionType = s.SectionType ?? string.Empty,
                Title = s.Title ?? string.Empty,
                ContentJson = s.ContentJson ?? string.Empty
            }).ToList(),
        JobRequiredSkills = (bd.SkillAlignment.Details ?? [])
            .Select(d => new JobSkillContext
            {
                SkillName = d.RequiredSkill,
                ImportanceScore = d.VectorSimilarityScore,
                IsRequired = true
            }).ToList(),
        JobPreferredSkills = []
    };
}
