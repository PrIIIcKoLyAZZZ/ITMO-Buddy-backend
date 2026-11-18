using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using ITMO.Buddy.Infrastructure.Persistence.Queries;
using ITMO.Buddy.Infrastructure.Persistence.Repositories;
using ITMO.Buddy.Infrastructure.Persistence.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMO.Buddy.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString =
            configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is missing. " +
                "Configure ConnectionStrings:Postgres.");
        }

        // DbContext

        services.AddDbContext<BuddyDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        // Shared persistence state

        services.AddScoped<ProfileGoalsStore>();

        // Repositories

        services.AddScoped<IAccountMaintenanceRepository, AccountMaintenanceRepository>();
        services.AddScoped<IFileCleanupQueue, FileCleanupQueue>();

        services.AddScoped<
            IUserRepository,
            UserRepository>();

        services.AddScoped<
            IUserProfileRepository,
            UserProfileRepository>();

        services.AddScoped<
            IRefreshTokenRepository,
            RefreshTokenRepository>();

        services.AddScoped<
            IReferenceDataRepository,
            ReferenceDataRepository>();

        services.AddScoped<
            IListingRepository,
            ListingRepository>();

        services.AddScoped<
            IUserLikeRepository,
            UserLikeRepository>();

        services.AddScoped<
            IUserMatchRepository,
            UserMatchRepository>();

        services.AddScoped<
            IUserBlockRepository,
            UserBlockRepository>();

        services.AddScoped<
            IConversationRepository,
            ConversationRepository>();

        services.AddScoped<
            IMessageRepository,
            MessageRepository>();

        services.AddScoped<
            INotificationRepository,
            NotificationRepository>();

        // Read queries

        services.AddScoped<
            IUserSearchQueries,
            UserSearchQueries>();

        services.AddScoped<
            IRecommendationCandidateQueries,
            RecommendationCandidateQueries>();

        services.AddScoped<
            IListingSearchQueries,
            ListingSearchQueries>();

        // Unit of Work and transactions

        services.AddScoped<
            IUnitOfWork,
            BuddyUnitOfWork>();

        services.AddScoped<
            ITransactionRunner,
            BuddyTransactionRunner>();

        return services;
    }
}