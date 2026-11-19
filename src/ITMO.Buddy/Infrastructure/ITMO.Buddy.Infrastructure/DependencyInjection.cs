using System.Security.Cryptography;
using ITMO.Buddy.Application.Abstractions.Security;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Infrastructure.FileCleanup;
using ITMO.Buddy.Infrastructure.Security;
using ITMO.Buddy.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ITMO.Buddy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddValidatedOptions<JwtOptions>(services, configuration.GetSection("Jwt"),
            static options => CryptographicOperations.ZeroMemory(options.ValidateAndReadSigningKey()));
        AddValidatedOptions<MinioOptions>(services, configuration.GetSection("Minio"),
            static options => options.ValidateAndReadEndpoints());
        services.AddOptions<MinioOptions>().PostConfigure<IHostEnvironment>(
            static (options, environment) => options.IsDevelopment = environment.IsDevelopment());
        AddValidatedOptions<FileCleanupOptions>(services, configuration.GetSection("FileCleanup"),
            static options => options.Validate());

        services.TryAddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.TryAddSingleton<ITokenService, JwtTokenService>();
        services.TryAddSingleton<MinioFileStorage>();
        services.TryAddSingleton<IFileStorage>(static provider => provider.GetRequiredService<MinioFileStorage>());
        services.AddHostedService<FileCleanupBackgroundService>();

        return services;
    }

    private static void AddValidatedOptions<TOptions>(
        IServiceCollection services, IConfiguration configuration, Action<TOptions> validate)
        where TOptions : class
    {
        services.AddSingleton<IValidateOptions<TOptions>>(new InfrastructureOptionsValidator<TOptions>(validate));
        services.AddOptions<TOptions>().Bind(configuration).ValidateOnStart();
    }

    private sealed class InfrastructureOptionsValidator<TOptions>(Action<TOptions> validate) : IValidateOptions<TOptions>
        where TOptions : class
    {
        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            if (name is not null && name != Options.DefaultName)
            {
                return ValidateOptionsResult.Skip;
            }

            try
            {
                validate(options);
                return ValidateOptionsResult.Success;
            }
            catch (ArgumentException error)
            {
                return ValidateOptionsResult.Fail(error.Message);
            }
        }
    }
}
