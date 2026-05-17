using GetJobAI.Optimisation.Api.Requests;
using GetJobAI.Optimisation.Api.Responses;
using GetJobAI.Optimisation.Contracts;
using GetJobAI.Optimisation.Data;
using GetJobAI.Optimisation.OptimisationService.Contexts;
using GetJobAI.Optimisation.Services;
using Microsoft.EntityFrameworkCore;
using Entities = GetJobAI.Optimisation.Data.Entities;

namespace GetJobAI.Optimisation.Api;

public static class OptimisationsEndpoints
{
    public static IEndpointRouteBuilder MapOptimisationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/optimisations/{optimisationId:guid}");

        group.MapPost("/work-experiences/{suggestionId:guid}/review", ReviewWorkExperience)
            .WithTags("Work Experiences")
            .WithSummary("Review a work experience suggestion")
            .WithDescription("Accept or reject an AI-generated work experience suggestion. " +
                             "A rejection hint is stored and passed to the AI on the next rewrite.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/work-experiences/{suggestionId:guid}/rewrite", RewriteWorkExperience)
            .WithTags("Work Experiences")
            .WithSummary("Rewrite a work experience suggestion")
            .WithDescription("Triggers an AI rewrite of the work experience entry. Replaces all existing bullets with " +
                             "the new output. An optional hint guides the AI.")
            .Produces<WorkExperienceRewriteResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

        group.MapPost("/bullets/{bulletId:guid}/review", ReviewBullet)
            .WithTags("Bullets")
            .WithSummary("Review a bullet point")
            .WithDescription("Accept or reject an individual AI-rewritten bullet point within a work experience suggestion.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/activities/{suggestionId:guid}/review", ReviewActivity)
            .WithTags("Activities")
            .WithSummary("Review an activity suggestion")
            .WithDescription("Accept or reject an AI-generated activity suggestion. A rejection hint is stored " +
                             "and passed to the AI on the next rewrite.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/activities/{suggestionId:guid}/rewrite", RewriteActivity)
            .WithTags("Activities")
            .WithSummary("Rewrite an activity suggestion")
            .WithDescription("Triggers an AI rewrite of the activity entry. An optional hint guides the AI.")
            .Produces<ActivityRewriteResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

        group.MapPost("/cover-letter/generate", GenerateCoverLetter)
            .WithTags("Cover Letter")
            .WithSummary("Generate a cover letter")
            .WithDescription("Generates or regenerates a cover letter using Gemini AI. Uses the session's accepted summary, " +
                             "skills, and top bullets automatically. If a cover letter already exists it is overwritten.")
            .Produces<CoverLetterResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

        group.MapGet("/cover-letter", GetCoverLetter)
            .WithTags("Cover Letter")
            .WithSummary("Get the cover letter")
            .WithDescription("Returns the saved cover letter for the optimisation session.")
            .Produces<CoverLetterResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ReviewWorkExperience(
        Guid optimisationId,
        Guid suggestionId,
        ReviewRequest request,
        OptimisationDbContext db,
        CancellationToken ct)
    {
        var suggestion = await db.WorkExperienceSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.OptimisationId == optimisationId, ct);

        if (suggestion is null)
        {
            return Results.NotFound();
        }

        suggestion.Accepted = request.Accepted;
        suggestion.RejectionHint = request.Accepted ? null : request.Hint;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> RewriteWorkExperience(
        Guid optimisationId,
        Guid suggestionId,
        RewriteRequest request,
        OptimisationDbContext db,
        IPromptRunner promptRunner,
        OptimisationContextFactory contextFactory,
        CancellationToken ct)
    {
        var suggestion = await db.WorkExperienceSuggestions
            .Include(s => s.Bullets)
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.OptimisationId == optimisationId, ct);
        
        if (suggestion is null) return Results.NotFound();

        var ctx = await contextFactory.CreateAsync(optimisationId, ct);
        if (ctx is null) return Results.NotFound();

        var entry = ctx.WorkExperiences.FirstOrDefault(we => we.EntryId == suggestion.EntryId);
        if (entry is null) return Results.NotFound();

        var result = await promptRunner.RewriteExperienceAsync(entry, ctx, ct, request.Hint);
        if (!result.Success)
            return Results.Problem("AI rewrite failed", statusCode: 502);

        db.BulletSuggestions.RemoveRange(suggestion.Bullets);

        var newBullets = result.Content.Bullets
            .Select(b => Entities.OptimisationBulletSuggestion.Create(
                suggestion.Id,
                b.Original,
                b.Rewritten,
                b.KeywordsAdded,
                b.XyzApplied))
            .ToList();

        db.BulletSuggestions.AddRange(newBullets);

        suggestion.Include = result.Content.Include;
        suggestion.Reason = result.Content.Reason;
        suggestion.Accepted = null;
        suggestion.RejectionHint = null;
        suggestion.RewriteCount++;

        await db.SaveChangesAsync(ct);

        var response = new WorkExperienceRewriteResponse(
            suggestion.Id,
            suggestion.EntryId,
            suggestion.Include,
            suggestion.Reason,
            suggestion.RewriteCount,
            newBullets.Select(b => new BulletSuggestionResponse(
                b.Id, b.Original, b.Rewritten, b.KeywordsAdded, b.XyzApplied, b.Accepted)).ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> ReviewBullet(
        Guid optimisationId,
        Guid bulletId,
        ReviewRequest request,
        OptimisationDbContext db,
        CancellationToken ct)
    {
        var bullet = await db.BulletSuggestions
            .Include(b => b.WorkExperienceSuggestion)
            .FirstOrDefaultAsync(b => b.Id == bulletId && b.WorkExperienceSuggestion.OptimisationId == optimisationId, ct);

        if (bullet is null)
        {
            return Results.NotFound();
        }

        bullet.Accepted = request.Accepted;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ReviewActivity(
        Guid optimisationId,
        Guid suggestionId,
        ReviewRequest request,
        OptimisationDbContext db,
        CancellationToken ct)
    {
        var suggestion = await db.ActivitySuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.OptimisationId == optimisationId, ct);

        if (suggestion is null)
        {
            return Results.NotFound();
        }

        suggestion.Accepted = request.Accepted;
        suggestion.RejectionHint = request.Accepted ? null : request.Hint;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> RewriteActivity(
        Guid optimisationId,
        Guid suggestionId,
        RewriteRequest request,
        OptimisationDbContext db,
        IPromptRunner promptRunner,
        OptimisationContextFactory contextFactory,
        CancellationToken ct)
    {
        var suggestion = await db.ActivitySuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.OptimisationId == optimisationId, ct);
        if (suggestion is null) return Results.NotFound();

        var ctx = await contextFactory.CreateAsync(optimisationId, ct);
        if (ctx is null) return Results.NotFound();

        var activity = ctx.Activities.FirstOrDefault(a => a.EntryId == suggestion.EntryId);
        if (activity is null) return Results.NotFound();

        var result = await promptRunner.RewriteActivityAsync(activity, ctx, ct, request.Hint);
        if (!result.Success)
            return Results.Problem("AI rewrite failed", statusCode: 502);

        suggestion.Include = result.Content.Include;
        suggestion.Reason = result.Content.Reason;
        suggestion.HighlightsRewritten = result.Content.HighlightsRewritten;
        suggestion.Accepted = null;
        suggestion.RejectionHint = null;
        suggestion.RewriteCount++;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ActivityRewriteResponse(
            suggestion.Id,
            suggestion.EntryId,
            suggestion.Include,
            suggestion.Reason,
            suggestion.HighlightsRewritten,
            suggestion.RewriteCount));
    }

    private static async Task<IResult> GenerateCoverLetter(
        Guid optimisationId,
        GenerateCoverLetterRequest request,
        OptimisationDbContext db,
        IPromptRunner promptRunner,
        OptimisationContextFactory contextFactory,
        CancellationToken ct)
    {
        var ctx = await contextFactory.CreateAsync(optimisationId, ct);
        if (ctx is null) return Results.NotFound();

        var summarySuggestion = await db.SummarySuggestions
            .FirstOrDefaultAsync(s => s.OptimisationId == optimisationId, ct);

        var acceptedSummary = summarySuggestion?.Rewritten
            ?? summarySuggestion?.Original
            ?? ctx.ExistingSummary
            ?? string.Empty;

        var topAchievements = request.TopAchievements?.Count > 0
            ? request.TopAchievements
            : await db.WorkExperienceSuggestions
                .Include(we => we.Bullets)
                .Where(we => we.OptimisationId == optimisationId)
                .SelectMany(we => we.Bullets.Select(b => b.Rewritten))
                .Take(5)
                .ToListAsync(ct);

        var existing = await db.CoverLetters
            .FirstOrDefaultAsync(cl => cl.OptimisationId == optimisationId, ct);

        var coverLetterCtx = new CoverLetterContext
        {
            JobTitle = ctx.JobTitle,
            CompanyName = ctx.CompanyName,
            CompanyDescription = request.CompanyDescription ?? string.Empty,
            CandidateName = ctx.CandidateName,
            AcceptedSummary = acceptedSummary,
            TopAchievements = topAchievements,
            AcceptedSkills = ctx.Skills.Select(s => s.SkillName).ToList(),
            MissingKeywords = ctx.MissingKeywords,
            Language = ctx.DetectedLanguage ?? "en-GB",
            CustomNote = request.CustomNote,
            RewriteCount = existing?.RewriteCount ?? 0
        };

        var result = await promptRunner.GenerateCoverLetterAsync(coverLetterCtx, ct);

        if (!result.Success)
        {
            return Results.Problem("AI generation failed", statusCode: 502);
        }

        var cl = result.Content;

        if (existing is not null)
        {
            existing.Update(cl.CoverLetter, cl.WordCount, cl.SalutationUsed, cl.KeyPointsMade);
            existing.RewriteCount++;
        }
        else
        {
            existing = Entities.OptimisationCoverLetter.Create(
                optimisationId, cl.CoverLetter, cl.WordCount, cl.SalutationUsed, cl.KeyPointsMade);
            db.CoverLetters.Add(existing);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToResponse(existing));
    }

    private static async Task<IResult> GetCoverLetter(
        Guid optimisationId,
        OptimisationDbContext db,
        CancellationToken ct)
    {
        var existing = await db.CoverLetters
            .FirstOrDefaultAsync(cl => cl.OptimisationId == optimisationId, ct);

        return existing is null ? Results.NotFound() : Results.Ok(ToResponse(existing));
    }

    private static CoverLetterResponse ToResponse(Entities.OptimisationCoverLetter cl) =>
        new(cl.CoverLetter, cl.WordCount, cl.SalutationUsed, cl.KeyPointsMade, cl.Accepted, cl.RewriteCount, cl.GeneratedAt);
}
