using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.API;

public static class Authentication
{
    public static IServiceCollection AddClaimsAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((options, settings) =>
        {
            var issuer = settings["Auth:Jwt:Issuer"] ?? throw new InvalidOperationException("Auth:Jwt:Issuer is required.");
            var audience = settings["Auth:Jwt:Audience"] ?? throw new InvalidOperationException("Auth:Jwt:Audience is required.");
            var key = SigningKey(settings);
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "name",
                RoleClaimType = "role"
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("HandlerOrAbove", policy => policy.RequireRole(nameof(UserRole.Handler), nameof(UserRole.Supervisor), nameof(UserRole.Manager)));
            options.AddPolicy("SupervisorOrAbove", policy => policy.RequireRole(nameof(UserRole.Supervisor), nameof(UserRole.Manager)));
            options.AddPolicy("ManagerOnly", policy => policy.RequireRole(nameof(UserRole.Manager)));
        });
        return services;
    }

    public static string CreateToken(UserDto user, Guid organisationId, IConfiguration configuration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey(configuration)));
        var token = new JwtSecurityToken(configuration["Auth:Jwt:Issuer"], configuration["Auth:Jwt:Audience"],
            [new Claim("sub", user.Id.ToString("D")), new Claim("name", user.Name),
             new Claim("role", user.Role.ToString()), new Claim("org_id", organisationId.ToString("D"))],
            expires: DateTime.UtcNow.AddMinutes(configuration.GetValue<int?>("Auth:Jwt:ExpirationMinutes") ?? 60),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string SigningKey(IConfiguration configuration)
    {
        var key = configuration["Auth:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("__SET_", StringComparison.Ordinal)
            || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Auth:Jwt:SigningKey must be configured with a non-placeholder secret of at least 32 bytes.");
        return key;
    }
}

public sealed record DevLoginRequest(string Username);
