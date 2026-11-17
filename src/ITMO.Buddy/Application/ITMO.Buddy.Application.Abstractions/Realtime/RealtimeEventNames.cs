namespace ITMO.Buddy.Application.Abstractions.Realtime;

/// <summary>
/// Стабильные имена событий реального времени,
/// передаваемых клиентам через SignalR.
/// </summary>
public static class RealtimeEventNames
{
    public const string MessageReceived =
        "MessageReceived";

    public const string MatchCreated =
        "MatchCreated";

    public const string ListingResponseCreated =
        "ListingResponseCreated";

    public const string ListingResponseReviewed =
        "ListingResponseReviewed";

    public const string NotificationUnreadCountChanged =
        "NotificationUnreadCountChanged";
}