using GetJobAI.Optimisation.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GetJobAI.Optimisation.Api;

public class DatabaseHealthCheck(OptimisationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            await db.Database.CanConnectAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
