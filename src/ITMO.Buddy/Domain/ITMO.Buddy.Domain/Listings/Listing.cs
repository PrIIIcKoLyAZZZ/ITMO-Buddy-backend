using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Domain.Listings;

public sealed class Listing : Entity
{
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 2000;
    public const int MinParticipantsCount = 1;
    public const int MaxParticipantsCount = 10;

    private readonly List<Skill> _requiredSkills = new();
    private readonly List<ListingResponse> _responses = new();

    public Guid AuthorId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public GoalCategory Category { get; private set; }

    public int RequiredParticipantsCount { get; private set; }

    public int AcceptedParticipantsCount { get; private set; }

    public ListingFormat Format { get; private set; }

    public DateTimeOffset? DeadlineUtc { get; private set; }

    public DateTimeOffset? EventAtUtc { get; private set; }

    public ListingStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Skill> RequiredSkills =>
        _requiredSkills.AsReadOnly();

    public IReadOnlyCollection<ListingResponse> Responses =>
        _responses.AsReadOnly();

    public bool IsOpen => Status == ListingStatus.Open;

    public bool HasAvailablePlaces =>
        AcceptedParticipantsCount < RequiredParticipantsCount;

    private Listing()
    {
    }

    public Listing(
        Guid id,
        Guid authorId,
        string title,
        string description,
        GoalCategory category,
        int requiredParticipantsCount,
        ListingFormat format,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? deadlineUtc = null,
        DateTimeOffset? eventAtUtc = null)
        : base(id)
    {
        if (authorId == Guid.Empty)
        {
            throw new DomainException(
                "listing.author_id.empty",
                "Author identifier cannot be empty.");
        }

        AuthorId = authorId;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));

        UpdatedAtUtc = CreatedAtUtc;
        Status = ListingStatus.Open;

        SetDetails(
            title,
            description,
            category,
            requiredParticipantsCount,
            format,
            deadlineUtc,
            eventAtUtc,
            CreatedAtUtc);
    }

    public void UpdateDetails(
        string title,
        string description,
        GoalCategory category,
        int requiredParticipantsCount,
        ListingFormat format,
        DateTimeOffset? deadlineUtc,
        DateTimeOffset? eventAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureOpen();

        var timestamp = RequireOperationTime(updatedAtUtc);

        SetDetails(
            title,
            description,
            category,
            requiredParticipantsCount,
            format,
            deadlineUtc,
            eventAtUtc,
            timestamp);

        UpdatedAtUtc = timestamp;
    }

    public void AddRequiredSkill(
        Skill skill,
        DateTimeOffset updatedAtUtc)
    {
        EnsureOpen();

        var timestamp = RequireOperationTime(updatedAtUtc);

        ValidateSkill(skill);

        if (_requiredSkills.Any(x =>
            x.Id == skill.Id ||
            x.NormalizedName == skill.NormalizedName))
        {
            throw new DomainException(
                "listing.skill.duplicate",
                "Required skill is already selected.");
        }

        _requiredSkills.Add(skill);
        UpdatedAtUtc = timestamp;
    }

    public bool RemoveRequiredSkill(
        Guid skillId,
        DateTimeOffset updatedAtUtc)
    {
        EnsureOpen();

        var timestamp = RequireOperationTime(updatedAtUtc);

        if (skillId == Guid.Empty)
        {
            throw new DomainException(
                "listing.skill_id.empty",
                "Skill identifier cannot be empty.");
        }

        var skill = _requiredSkills.FirstOrDefault(
            x => x.Id == skillId);

        if (skill is null)
        {
            return false;
        }

        _requiredSkills.Remove(skill);
        UpdatedAtUtc = timestamp;

        return true;
    }

    public void SetRequiredSkills(
        IEnumerable<Skill> skills,
        DateTimeOffset updatedAtUtc)
    {
        EnsureOpen();

        var timestamp = RequireOperationTime(updatedAtUtc);

        if (skills is null)
        {
            throw new DomainException(
                "listing.skills.null",
                "Required skills collection cannot be null.");
        }

        var newSkills = skills.ToList();

        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var skill in newSkills)
        {
            ValidateSkill(skill);

            if (!ids.Add(skill.Id) ||
                !names.Add(skill.NormalizedName))
            {
                throw new DomainException(
                    "listing.skill.duplicate",
                    "Required skills contain duplicates.");
            }
        }

        _requiredSkills.Clear();
        _requiredSkills.AddRange(newSkills);

        UpdatedAtUtc = timestamp;
    }

    public bool CanRespond(
        Guid candidateId,
        DateTimeOffset nowUtc)
    {
        var timestamp = UtcTime.Require(
            nowUtc,
            nameof(nowUtc));

        return candidateId != Guid.Empty &&
               candidateId != AuthorId &&
               IsOpen &&
               HasAvailablePlaces &&
               IsWithinResponseWindow(timestamp) &&
               !_responses.Any(x => x.CandidateId == candidateId);
    }

    public ListingResponse SubmitResponse(
        Guid responseId,
        Guid candidateId,
        string? message,
        DateTimeOffset createdAtUtc)
    {
        var timestamp = RequireOperationTime(createdAtUtc);

        EnsureOpen();

        if (candidateId == Guid.Empty)
        {
            throw new DomainException(
                "listing.response.candidate_id.empty",
                "Candidate identifier cannot be empty.");
        }

        if (candidateId == AuthorId)
        {
            throw new DomainException(
                "listing.response.self_response",
                "Author cannot respond to their own listing.");
        }

        if (!HasAvailablePlaces)
        {
            throw new DomainException(
                "listing.participants.full",
                "Listing has no available places.");
        }

        EnsureResponseWindow(timestamp);

        if (_responses.Any(x => x.CandidateId == candidateId))
        {
            throw new DomainException(
                "listing.response.duplicate",
                "Candidate has already responded to this listing.");
        }

        var response = new ListingResponse(
            responseId,
            Id,
            candidateId,
            message,
            timestamp);

        _responses.Add(response);
        UpdatedAtUtc = timestamp;

        return response;
    }

    public bool CanAcceptResponse(
        Guid responseId,
        DateTimeOffset nowUtc)
    {
        var timestamp = UtcTime.Require(
            nowUtc,
            nameof(nowUtc));

        if (!IsOpen ||
            !HasAvailablePlaces ||
            !IsWithinResponseWindow(timestamp))
        {
            return false;
        }

        var response = _responses.FirstOrDefault(
            x => x.Id == responseId);

        return response is not null && response.IsPending;
    }

    public void AcceptResponse(
        Guid responseId,
        DateTimeOffset decidedAtUtc)
    {
        var timestamp = RequireOperationTime(decidedAtUtc);

        EnsureOpen();

        if (!HasAvailablePlaces)
        {
            throw new DomainException(
                "listing.participants.full",
                "Listing has no available places.");
        }

        EnsureResponseWindow(timestamp);

        var response = GetResponse(responseId);

        response.Accept(timestamp);

        AcceptedParticipantsCount++;
        UpdatedAtUtc = timestamp;
    }

    public void RejectResponse(
        Guid responseId,
        DateTimeOffset decidedAtUtc)
    {
        var timestamp = RequireOperationTime(decidedAtUtc);

        var response = GetResponse(responseId);

        response.Reject(timestamp);

        UpdatedAtUtc = timestamp;
    }

    public void Close(DateTimeOffset closedAtUtc)
    {
        var timestamp = RequireOperationTime(closedAtUtc);

        if (!IsOpen)
        {
            return;
        }

        Status = ListingStatus.Closed;
        UpdatedAtUtc = timestamp;
    }

    private ListingResponse GetResponse(Guid responseId)
    {
        if (responseId == Guid.Empty)
        {
            throw new DomainException(
                "listing.response_id.empty",
                "Response identifier cannot be empty.");
        }

        return _responses.FirstOrDefault(x => x.Id == responseId)
            ?? throw new DomainException(
                "listing.response.not_found",
                "Response does not belong to this listing.");
    }

    private void SetDetails(
        string title,
        string description,
        GoalCategory category,
        int requiredParticipantsCount,
        ListingFormat format,
        DateTimeOffset? deadlineUtc,
        DateTimeOffset? eventAtUtc,
        DateTimeOffset referenceUtc)
    {
        var normalizedTitle = title?.Trim();
        var normalizedDescription = description?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle) ||
            normalizedTitle.Length > MaxTitleLength)
        {
            throw new DomainException(
                "listing.title.invalid",
                $"Title must contain between 1 and {MaxTitleLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(normalizedDescription) ||
            normalizedDescription.Length > MaxDescriptionLength)
        {
            throw new DomainException(
                "listing.description.invalid",
                $"Description must contain between 1 and {MaxDescriptionLength} characters.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException(
                "listing.category.invalid",
                "Unknown listing category.");
        }

        if (!Enum.IsDefined(format))
        {
            throw new DomainException(
                "listing.format.invalid",
                "Unknown listing format.");
        }

        if (requiredParticipantsCount < MinParticipantsCount ||
            requiredParticipantsCount > MaxParticipantsCount)
        {
            throw new DomainException(
                "listing.participants.invalid",
                $"Required participants count must be between {MinParticipantsCount} and {MaxParticipantsCount}.");
        }

        if (requiredParticipantsCount < AcceptedParticipantsCount)
        {
            throw new DomainException(
                "listing.participants.below_accepted",
                "Required participants count cannot be less than accepted participants count.");
        }

        ValidateDates(
            deadlineUtc,
            eventAtUtc,
            referenceUtc);

        Title = normalizedTitle;
        Description = normalizedDescription;
        Category = category;
        RequiredParticipantsCount = requiredParticipantsCount;
        Format = format;
        DeadlineUtc = deadlineUtc;
        EventAtUtc = eventAtUtc;
    }

    private static void ValidateDates(
        DateTimeOffset? deadlineUtc,
        DateTimeOffset? eventAtUtc,
        DateTimeOffset referenceUtc)
    {
        if (deadlineUtc.HasValue)
        {
            var deadline = UtcTime.Require(
                deadlineUtc.Value,
                nameof(deadlineUtc));

            if (deadline <= referenceUtc)
            {
                throw new DomainException(
                    "listing.deadline.invalid",
                    "Deadline must be later than the operation time.");
            }
        }

        if (eventAtUtc.HasValue)
        {
            var eventDate = UtcTime.Require(
                eventAtUtc.Value,
                nameof(eventAtUtc));

            if (eventDate <= referenceUtc)
            {
                throw new DomainException(
                    "listing.event_date.invalid",
                    "Event date must be in the future.");
            }
        }

        if (deadlineUtc.HasValue &&
            eventAtUtc.HasValue &&
            deadlineUtc.Value > eventAtUtc.Value)
        {
            throw new DomainException(
                "listing.dates.invalid_order",
                "Deadline cannot be later than the event date.");
        }
    }

    private bool IsWithinResponseWindow(
        DateTimeOffset nowUtc)
    {
        if (nowUtc < CreatedAtUtc)
        {
            return false;
        }

        if (DeadlineUtc.HasValue &&
            nowUtc >= DeadlineUtc.Value)
        {
            return false;
        }

        if (EventAtUtc.HasValue &&
            nowUtc >= EventAtUtc.Value)
        {
            return false;
        }

        return true;
    }

    private void EnsureResponseWindow(
        DateTimeOffset nowUtc)
    {
        if (!IsWithinResponseWindow(nowUtc))
        {
            throw new DomainException(
                "listing.response.window_closed",
                "Listing no longer accepts responses.");
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException(
                "listing.closed",
                "Operation is not allowed for a closed listing.");
        }
    }

    private DateTimeOffset RequireOperationTime(
        DateTimeOffset nowUtc)
    {
        var timestamp = UtcTime.Require(
            nowUtc,
            nameof(nowUtc));

        if (timestamp < CreatedAtUtc ||
            timestamp < UpdatedAtUtc)
        {
            throw new DomainException(
                "listing.time.invalid",
                "Operation time cannot precede listing timestamps.");
        }

        return timestamp;
    }

    private static void ValidateSkill(Skill skill)
    {
        if (skill is null ||
            skill.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(skill.NormalizedName))
        {
            throw new DomainException(
                "listing.skill.invalid",
                "Required skill is invalid.");
        }
    }
}