using GetJobAI.Optimisation.Contracts;
using GetJobAI.Optimisation.Data;
using GetJobAI.Optimisation.Messaging.Consumers;
using GetJobAI.Optimisation.OptimisationService;
using GetJobAI.Optimisation.OptimisationService.MetricsCollector;
using GetJobAI.Optimisation.Prompts;
using Google.GenAI;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/system-.txt", 
        rollingInterval: RollingInterval.Day, 
        restrictedToMinimumLevel: LogEventLevel.Warning)
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt => 
            evt.Properties.ContainsKey("SourceContext") && 
            evt.Properties["SourceContext"].ToString().Contains("Prompt"))
        .WriteTo.File(
            "logs/prompt-audit-.txt", 
            rollingInterval: RollingInterval.Day))
    .CreateLogger();

builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

var geminiOptions = builder.Configuration
    .GetSection(GeminiOptions.SectionName)
    .Get<GeminiOptions>();

if (geminiOptions?.ApiKey is not null)
{
    builder.Services.AddSingleton(new Client(apiKey: geminiOptions.ApiKey));
}

builder.Services.AddSingleton<IPromptRegistry, PromptRegistry>();

builder.Services.AddSingleton<PromptMetricsCollector>();

builder.Services.AddScoped<PromptRunner>();
builder.Services.AddScoped<IPromptRunner>(sp =>
    new LoggingPromptRunner(
        sp.GetRequiredService<PromptRunner>(),
        sp.GetRequiredService<PromptMetricsCollector>(),
        sp.GetRequiredService<ILogger<LoggingPromptRunner>>()));

builder.Services.AddScoped<IOptimisationOrchestrator, OptimisationOrchestrator>();

builder.Services.AddDbContext<OptimisationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ResumeScoredConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "rabbitmq://localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
        
        cfg.UseRawJsonSerializer();

        cfg.ReceiveEndpoint("ai-optimizer.resume.scored", e =>
        {
            e.ConfigureConsumer<ResumeScoredConsumer>(context);
        });
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.Run();

