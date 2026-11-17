using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Domain.Listings;

public sealed class ListingResponse : Entity
{
    public const int MaxMessageLength = 500;

    public Guid ListingId { get; private set; }

    public Guid CandidateId { get; private set; }

    public string? Message { get; private set; }

    public ListingResponseStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public bool IsPending =>
        Status == ListingResponseStatus.Pending;

    private ListingResponse()
    {
    }

    internal ListingResponse(
        Guid id,
        Guid listingId,
        Guid candidateId,
        string? message,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (listingId == Guid.Empty)
        {
            throw new DomainException(
                "listing_response.listing_id.empty",
                "Listing identifier cannot be empty.");
        }

        if (candidateId == Guid.Empty)
        {
            throw new DomainException(
                "listing_response.candidate_id.empty",
                "Candidate identifier cannot be empty.");
        }

        var normalizedMessage = message?.Trim();

        if (normalizedMessage?.Length > MaxMessageLength)
        {
            throw new DomainException(
                "listing_response.message.too_long",
                $"Response message cannot exceed {MaxMessageLength} characters.");
        }

        ListingId = listingId;
        CandidateId = candidateId;

        Message = string.IsNullOrWhiteSpace(normalizedMessage)
            ? null
            : normalizedMessage;

        Status = ListingResponseStatus.Pending;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    internal void Accept(DateTimeOffset decidedAtUtc)
    {
        Decide(ListingResponseStatus.Accepted, decidedAtUtc);
    }

    internal void Reject(DateTimeOffset decidedAtUtc)
    {
        Decide(ListingResponseStatus.Rejected, decidedAtUtc);
    }

    private void Decide(
        ListingResponseStatus newStatus,
        DateTimeOffset decidedAtUtc)
    {
        if (!IsPending)
        {
            throw new DomainException(
                "listing_response.already_decided",
                "Response has already been processed.");
        }

        var timestamp = UtcTime.Require(
            decidedAtUtc,
            nameof(decidedAtUtc));

        if (timestamp < CreatedAtUtc)
        {
            throw new DomainException(
                "listing_response.decision.invalid_time",
                "Decision cannot precede response creation.");
        }

        Status = newStatus;
        DecidedAtUtc = timestamp;
    }
}