using ClaimsModule.API;
using ClaimsModule.Application;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Infrastructure;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Seeding;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    ProductionConfiguration.Validate(builder.Configuration, builder.Environment);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressMapClientErrors = true;
            // Binding errors must use the same middleware contract as command validation.
            options.InvalidModelStateResponseFactory = context => throw new ModelBindingException(
                context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key,
                    x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "The supplied value is invalid." : e.ErrorMessage).ToArray()));
        });
    const long multipartRequestLimit = 52L * 1024 * 1024; // 50 MB file plus multipart overhead. Azure App Service request limits must also be configured at the hosting layer.
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = multipartRequestLimit);
    builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = multipartRequestLimit);
    builder.Services.AddSwaggerGen(options =>
    {
        var xml = Path.Combine(AppContext.BaseDirectory, "ClaimsModule.API.xml");
        if (File.Exists(xml)) options.IncludeXmlComments(xml);
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        options.AddSecurityRequirement(document => new() { [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = [] });
        options.OperationFilter<SwaggerDocumentationOperationFilter>();
    });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence();
    builder.Services.AddClaimsBackgroundJobs(builder.Environment.IsEnvironment("Testing"));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddSingleton<IRequestLog, SerilogRequestLog>();
    builder.Services.AddClaimsAuthentication();
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Azure App Service proxy addresses are not fixed in the application configuration.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
    builder.Services.AddCors(options => options.AddPolicy("Angular", policy => policy
        .WithOrigins(ProductionConfiguration.GetCorsOrigins(builder.Configuration))
        .AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();
    if (app.Environment.IsProduction() && app.Configuration.GetValue<bool>("Auth:DevLoginEnabled"))
        ProductionConfiguration.LogDevLoginWarning(app.Logger);

    app.UseForwardedHeaders();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ApiExceptionMiddleware>();
    app.UseCors("Angular");
    app.UseAuthentication();
    app.UseMiddleware<UserLogContextMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();
    app.UseAuthorization();
    app.UseClaimsHangfireDashboard();
    app.MapControllers();

    // D2 requires OpenAPI to remain available on the deployed review environment.
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("Healthy")))
        .WithName("Health")
        .WithTags("System");

    app.MapGet("/health/ready", async (ClaimsDbContext database, CancellationToken cancellationToken) =>
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            database.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
            return await database.Database.CanConnectAsync(timeout.Token)
                ? Results.Ok(new HealthResponse("Ready"))
                : Results.Json(new HealthResponse("DatabaseUnavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            ProductionConfiguration.LogReadinessWarning(app.Logger, exception);
            return Results.Json(new HealthResponse("DatabaseUnavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }).WithName("Readiness").WithTags("System");

    if (app.Configuration.GetValue<bool>("Auth:DevLoginEnabled"))
        app.MapPost("/api/auth/dev-login", async (DevLoginRequest request, IUserQueries users, IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var user = await users.FindByUsernameAsync(request.Username, cancellationToken);
            if (user is null) return Results.NotFound();
            return Results.Ok(new
            {
                accessToken = Authentication.CreateToken(user, SeedConstants.OrganisationId, configuration),
                user = new { id = user.Id, name = user.Name, role = user.Role.ToString() }
            });
        }).AllowAnonymous().WithTags("Auth").WithSummary("Issue a development JWT for a seeded user.")
            .Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound);

    if (app.Environment.IsEnvironment("Testing"))
    {
        app.MapGet("/__test/error/{kind}", (string kind) =>
        {
            throw kind switch
            {
                "validation" => new ValidationException([new FluentValidation.Results.ValidationFailure("Field", "Invalid value")]),
                "domain" => new DomainRuleViolationException("RULE", "Rule failed"),
                "self" => new SelfApprovalException(),
                "authority" => new InsufficientAuthorityException(),
                "missing" => new NotFoundException("Missing"),
                "conflict" => new ConcurrencyException("Conflict", new InvalidOperationException()),
                _ => new InvalidOperationException("Secret details")
            };
        }).ExcludeFromDescription();
        app.MapGet("/__test/supervisor", () => Results.Ok()).RequireAuthorization("SupervisorOrAbove").ExcludeFromDescription();
        app.MapGet("/__test/manager", () => Results.Ok()).RequireAuthorization("ManagerOnly").ExcludeFromDescription();
        app.MapGet("/__test/current", (ICurrentUser user) => Results.Ok(new
        {
            user.UserId,
            user.Name,
            user.Role,
            user.OrganisationId,
            user.CorrelationId
        })).RequireAuthorization().ExcludeFromDescription();
        app.MapPost("/__test/claim-audit", async (IUnitOfWork unit, IDomainEventDispatcher dispatcher,
            IClaimRepository claims, IClaimNumberGenerator numbers, IIdentifierGenerator ids, IClock clock,
            ICurrentUser user, CancellationToken cancellationToken) =>
        {
            var result = await unit.ExecuteAsync(async token =>
            {
                var id = ids.NewId();
                var loss = LossEvent.Create(ids.NewId(), id, clock.UtcNow.AddDays(-1),
                    "A vehicle was damaged during a road collision.", "COL-VEH-COL", true, clock);
                var claim = Claim.Create(id, await numbers.NextAsync(clock.UtcNow.Year, token), user.UserId!.Value,
                    loss, [ClaimParty.Create(ids.NewId(), id, PartyRole.Claimant, PartyType.Person, "Test", "Claimant")], clock);
                claims.Add(claim);
                return id;
            }, dispatcher, cancellationToken);
            return Results.Ok(new { claimId = result.Value });
        }).RequireAuthorization().ExcludeFromDescription();
    }

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "The API host terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
