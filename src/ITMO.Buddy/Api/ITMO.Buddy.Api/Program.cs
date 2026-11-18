using ITMO.Buddy.Api.Authentication;
using ITMO.Buddy.Api.Configuration;
using ITMO.Buddy.Api.Errors;
using ITMO.Buddy.Api.Realtime;
using ITMO.Buddy.Application;
using ITMO.Buddy.Application.Abstractions.Identity;
using ITMO.Buddy.Application.Abstractions.Realtime;
using ITMO.Buddy.Infrastructure;
using ITMO.Buddy.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddApiAuthentication();
builder.Services.AddApiAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddApiCors(builder.Configuration);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = context => ApiErrorResponses.WriteAsync(context, ApiErrorResponses.InternalError)
});
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
