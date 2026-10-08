using System.Text;

namespace ClaimsModule.API;

public static class ProductionConfiguration
{
    private static readonly Action<ILogger, Exception?> DevLoginWarning = LoggerMessage.Define(
        LogLevel.Warning, new EventId(2001, "ProductionDevLoginEnabled"),
        "Auth:DevLoginEnabled is enabled in Production. This username-only login is for the public assessment demo only.");
    private static readonly Action<ILogger, Exception?> ReadinessWarning = LoggerMessage.Define(
        LogLevel.Warning, new EventId(2002, "DatabaseReadinessFailed"), "Database readiness check failed.");

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;

        var errors = new List<string>();
        var signingKey = configuration["Auth:Jwt:SigningKey"];
        if (IsMissingOrPlaceholder(signingKey) || Encoding.UTF8.GetByteCount(signingKey!) < 32)
            errors.Add("Auth:Jwt:SigningKey must be configured with a non-placeholder secret of at least 32 bytes.");

        if (IsMissingOrPlaceholder(configuration.GetConnectionString("Default")))
            errors.Add("ConnectionStrings:Default must be configured.");

        if (configuration["Storage:Provider"]?.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (IsMissingOrPlaceholder(configuration["Storage:Azure:ConnectionString"]))
                errors.Add("Storage:Azure:ConnectionString must be configured when Storage:Provider is AzureBlob.");
            if (string.IsNullOrWhiteSpace(configuration["Storage:Azure:Container"]))
                errors.Add("Storage:Azure:Container must be configured when Storage:Provider is AzureBlob.");
        }

        var origins = GetCorsOrigins(configuration);
        if (origins.Length == 0)
            errors.Add("At least one Cors:Origins entry must be configured.");
        if (origins.Any(origin => origin.Contains('*', StringComparison.Ordinal)))
            errors.Add("Cors:Origins entries cannot contain wildcards in Production.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Production configuration is invalid: " + string.Join(" ", errors));
    }

    public static string[] GetCorsOrigins(IConfiguration configuration) =>
        configuration.GetSection("Cors:Origins").Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    public static void LogDevLoginWarning(ILogger logger) => DevLoginWarning(logger, null);

    public static void LogReadinessWarning(ILogger logger, Exception exception) => ReadinessWarning(logger, exception);

    private static bool IsMissingOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.StartsWith("__SET_", StringComparison.Ordinal);
}
