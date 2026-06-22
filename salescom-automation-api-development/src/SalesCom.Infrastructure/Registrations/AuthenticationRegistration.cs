namespace SalesCom.Infrastructure.Registrations;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JoseJwt = Jose.JWT;
using JweAlgorithm = Jose.JweAlgorithm;
using JweEncryption = Jose.JweEncryption;
using SalesCom.Application.Interfaces;
using SalesCom.Infrastructure.Authorization;
using SalesCom.Infrastructure.Configurations;
using SalesCom.Infrastructure.Gateways;
using SalesCom.Infrastructure.Services;

/// <summary>
/// Wires the Central Login client, our session/token issuance, JWT bearer validation, and the
/// per-request right authorization. Notable hooks:
/// <list type="bullet">
///   <item><b>OnMessageReceived</b> — unwraps the JWE envelope (A128KW / A128CBC_HS256) before
///   the standard JWT bearer handler validates the inner HS256 signature.</item>
///   <item><b>OnForbidden / OnChallenge</b> — emit the unified ApiResponse-shaped JSON.</item>
/// </list>
/// Credential verification and 2FA live in the Central Login service; <c>[HasRight(id)]</c>
/// checks the caller's granted rights from the DB on every protected request.
/// </summary>
public static class AuthenticationRegistration
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtConfiguration>()
            .Bind(configuration.GetSection(JwtConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CentralLoginConfiguration>()
            .Bind(configuration.GetSection(CentralLoginConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IUserRightsQuery, UserRightsQuery>();

        services.AddHttpClient<ICentralLoginClient, CentralLoginClient>((sp, client) =>
        {
            var central = sp.GetRequiredService<IOptions<CentralLoginConfiguration>>().Value;
            var baseUrl = central.BaseUrl.EndsWith('/') ? central.BaseUrl : central.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(central.TimeoutSeconds);
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtConfiguration>, ILoggerFactory>(ConfigureJwtBearer);

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, RightPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, RightAuthorizationHandler>();

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions bearer, IOptions<JwtConfiguration> jwtOptions, ILoggerFactory loggerFactory)
    {
        var jwt = jwtOptions.Value;
        var logger = loggerFactory.CreateLogger("SalesCom.JwtBearer");

        bearer.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
        bearer.SaveToken = true;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = jwt.ValidateIssuer,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = jwt.ValidateAudience,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
            NameClaimType = jwt.UserNameClaim,
        };

        var encryptionKey = Encoding.UTF8.GetBytes(jwt.EncryptionKey);

        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // The bearer header carries a JWE. Unwrap to the inner JWT before the handler
                // attempts signature validation. If decryption fails we leave Token unset and
                // the handler will reject the request — exactly the reference's behavior.
                if (!context.Request.Headers.TryGetValue("Authorization", out var header))
                {
                    return Task.CompletedTask;
                }

                var raw = header.ToString();
                if (string.IsNullOrEmpty(raw))
                {
                    return Task.CompletedTask;
                }

                var token = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? raw[7..].Trim() : raw.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    return Task.CompletedTask;
                }

                try
                {
                    var decoded = JoseJwt.Decode(token, encryptionKey, JweAlgorithm.A128KW, JweEncryption.A128CBC_HS256);
                    context.Token = decoded;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "JWE decode failed for inbound token.");
                    // Leave context.Token empty so the handler raises 401 via OnChallenge.
                }

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(
                    """{"success":false,"message":"A valid bearer token is required.","errorCode":"User.Unauthorized"}""");
            },

            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(
                    """{"success":false,"message":"You do not have permission to perform this action.","errorCode":"User.Forbidden"}""");
            },
        };
    }
}
