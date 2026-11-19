using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Api.Realtime;
using ITMO.Buddy.Application;
using ITMO.Buddy.Application.Abstractions.Identity;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Infrastructure;
using ITMO.Buddy.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Request-start and transport diagnostics can contain the Hub URL or connection token.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Warning);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = ApiErrorResponses.InvalidModelState;
    options.SuppressMapClientErrors = true;
});
builder.Services.AddScoped<AuthOriginGuard>();
builder.Services.AddApiAuthentication();
builder.Services.AddApiAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, JwtSignalRUserIdProvider>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration, builder.Environment.IsDevelopment());
builder.AddApiHttps();
var useTrustedForwarding = builder.AddTrustedForwarding();
builder.Services.AddApiOpenApi();
builder.Services.AddApiHealthChecks();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (useTrustedForwarding)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = context => ApiErrorResponses.WriteAsync(context, ApiErrorResponses.InternalError)
});
app.UseStatusCodePages(ApiErrorResponses.WriteEmptyClientErrorAsync);
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "ITMO Buddy API v1"));
    app.MapOpenApi().AllowAnonymous();
}

app.UseRouting();
app.UseMiddleware<ChatHubOriginGuard>();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>(ChatHub.Path, options => options.CloseOnAuthenticationExpiration = true);
app.MapApiHealthChecks();

app.Run();
