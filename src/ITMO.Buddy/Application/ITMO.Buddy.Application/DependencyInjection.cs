using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Profiles;
using ITMO.Buddy.Application.Abstractions.Recommendations;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Auth;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Dictionaries;
using ITMO.Buddy.Application.Profiles;
using ITMO.Buddy.Application.Contracts.Auth;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Contracts.Dictionaries;
using ITMO.Buddy.Application.Contracts.Listings;
using ITMO.Buddy.Application.Contracts.Notifications;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Recommendations;
using ITMO.Buddy.Application.Contracts.Responses;
using ITMO.Buddy.Application.Contracts.Search;
using ITMO.Buddy.Application.Conversations;
using ITMO.Buddy.Application.Interactions;
using ITMO.Buddy.Application.Listings;
using ITMO.Buddy.Application.Notifications;
using ITMO.Buddy.Application.Recommendations;
using ITMO.Buddy.Application.Search;
using ITMO.Buddy.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITMO.Buddy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.TryAddScoped<CurrentUserGuard>();
        services.TryAddScoped<OptionalPhotoUrl>();
        services.TryAddScoped<IAuthService, AuthService>();
        services.TryAddScoped<IProfileService, ProfileService>();
        services.TryAddScoped<IDictionaryService, DictionaryService>();
        services.TryAddScoped<IUserSearchService, UserSearchService>();
        services.TryAddScoped<IRecommendationService, RecommendationService>();
        services.TryAddScoped<IListingService, ListingService>();
        services.TryAddScoped<IListingResponseService, ListingResponseService>();
        services.TryAddScoped<ILikeService, LikeService>();
        services.TryAddScoped<IBlockService, BlockService>();
        services.TryAddScoped<IConversationService, ConversationService>();
        services.TryAddScoped<INotificationService, NotificationService>();
        services.TryAddScoped<UnreadNotificationDelivery>();
        services.TryAddScoped<ListingAccess>();
        services.TryAddScoped<ListingPresentation>();
        services.TryAddScoped<RealtimeDelivery>();
        services.TryAddScoped<ProfileCardMapper>();
        services.TryAddScoped<RecommendationScorer>();
        services.TryAddSingleton<IRecommendationSkipStore, MemoryRecommendationSkipStore>();

        services.AddOptions<RecommendationOptions>()
            .Validate(options => options.HasValidWeights(),
                "Recommendation weights must be finite, nonnegative and sum to 1.")
            .Validate(options => options.SkipDuration > TimeSpan.Zero,
                "Recommendation skip duration must be positive.")
            .ValidateOnStart();

        services.TryAddScoped<IValidator<ProfilePhotoUpload>, ProfilePhotoValidator>();

        services.TryAddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.TryAddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.TryAddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        services.TryAddScoped<IValidator<SearchUsersRequest>, SearchUsersRequestValidator>();
        services.TryAddScoped<IValidator<GetRecommendationsRequest>, GetRecommendationsRequestValidator>();
        services.TryAddScoped<IValidator<CreateListingRequest>, CreateListingRequestValidator>();
        services.TryAddScoped<IValidator<UpdateListingRequest>, UpdateListingRequestValidator>();
        services.TryAddScoped<IValidator<SearchListingsRequest>, SearchListingsRequestValidator>();
        services.TryAddScoped<IValidator<CreateListingResponseRequest>, CreateListingResponseRequestValidator>();
        services.TryAddScoped<IValidator<ReviewListingResponseRequest>, ReviewListingResponseRequestValidator>();
        services.TryAddScoped<IValidator<SendMessageRequest>, SendMessageRequestValidator>();
        services.TryAddScoped<IValidator<CreateDictionaryItemRequest>, CreateDictionaryItemRequestValidator>();
        services.TryAddScoped<IValidator<DictionarySearchRequest>, DictionarySearchRequestValidator>();
        services.TryAddScoped<IValidator<MarkNotificationsReadRequest>, MarkNotificationsReadRequestValidator>();
        services.TryAddScoped<IValidator<PaginationRequest>, PaginationRequestValidator>();

        return services;
    }
}
