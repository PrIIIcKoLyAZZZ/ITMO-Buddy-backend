using System.Globalization;
using ITMO.Buddy.Application.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace ITMO.Buddy.Api.Configuration;

internal static class OpenApiConfiguration
{
    private const string BearerScheme = "Bearer";

    public static void AddApiOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "ITMO Buddy API";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer(DescribeOperationAsync);
        });

    private static async Task DescribeOperationAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action)
        {
            return;
        }

        var anonymous = action.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (!anonymous)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = []
                }
            ];
        }
        else
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Origin",
                In = ParameterLocation.Header,
                Required = true,
                Description = "The API origin or an explicitly allowed frontend origin.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }

        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
            {
                if (parameter.In == ParameterLocation.Query &&
                    parameter.Schema?.Type?.HasFlag(JsonSchemaType.Array) == true)
                {
                    parameter.Style = ParameterStyle.Form;
                    parameter.Explode = true;
                    parameter.Description = "Repeat the query key once per value.";
                }
            }
        }

        var document = context.Document!;
        const string errorComponent = nameof(ErrorResponse);
        if (document.Components?.Schemas?.ContainsKey(errorComponent) != true)
        {
            document.AddComponent(errorComponent,
                await context.GetOrCreateSchemaAsync(typeof(ErrorResponse), null, cancellationToken));
        }

        operation.Responses ??= new OpenApiResponses();
        var statuses = new List<int> { 400, 401, 403, 404, 500 };
        if (!HttpMethods.IsGet(context.Description.HttpMethod ?? ""))
        {
            statuses.Add(409);
        }

        if (operation.RequestBody is not null)
        {
            statuses.Add(415);
        }

        if (operation.RequestBody?.Content?.TryGetValue("multipart/form-data", out var multipart) == true)
        {
            statuses.Add(413);
            if (multipart.Schema is OpenApiSchema form)
            {
                form.Required ??= new HashSet<string>();
                form.Required.Add("file");
            }
        }

        if (action.EndpointMetadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            statuses.Add(429);
        }

        foreach (var status in statuses)
        {
            operation.Responses[status.ToString(CultureInfo.InvariantCulture)] = new OpenApiResponse
            {
                Description = ReasonPhrases.GetReasonPhrase(status),
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = new OpenApiSchemaReference(errorComponent, document) }
                }
            };
        }
    }
}
