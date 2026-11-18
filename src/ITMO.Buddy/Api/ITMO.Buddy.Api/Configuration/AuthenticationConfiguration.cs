using System.Security.Claims;
using System.Text.Json;
using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ITMO.Buddy.Api.Configuration;

internal static class AuthenticationConfiguration
{
    public static void AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = HttpCurrentUser.SubjectClaimType,
                    RoleClaimType = HttpCurrentUser.RoleClaimType
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateIdentity,
                    OnChallenge = ApiErrorResponses.WriteChallengeAsync,
                    OnForbidden = context => ApiErrorResponses.WriteForbiddenAsync(context.HttpContext)
                };
            });
    }

    private static Task ValidateIdentity(TokenValidatedContext context)
    {
        if (context.SecurityToken is not JsonWebToken token || token.ValidFrom == DateTime.MinValue ||
            HttpCurrentUser.ReadIdentity(context.Principal?.Identity as ClaimsIdentity) is null ||
            !HasUnambiguousIdentity(token))
        {
            context.Fail("Invalid token identity or validity interval.");
        }

        return Task.CompletedTask;
    }

    private static bool HasUnambiguousIdentity(JsonWebToken token)
    {
        // Claims materialization can collapse duplicate JSON properties. Inspect the validated payload.
        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
        var subjects = 0;
        var roles = 0;
        foreach (var property in payload.RootElement.EnumerateObject())
        {
            if (property.NameEquals(HttpCurrentUser.SubjectClaimType))
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                subjects++;
            }
            else if (property.NameEquals(HttpCurrentUser.RoleClaimType))
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                roles++;
            }
        }

        return subjects == 1 && roles == 1;
    }
}
