using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Domain.Users;

public sealed class UserProfile : Entity
{
    public const int MaxAboutMeLength = 1000;
    public const int MaxInterestsCount = 15;
    public const int MaxSkillsCount = 15;

    private readonly List<GoalCategory> _goals = new();
    private readonly List<Interest> _interests = new();
    private readonly List<Skill> _skills = new();

    public Guid UserId { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? PhotoObjectKey { get; private set; }

    public Guid? FacultyId { get; private set; }

    public int? Course { get; private set; }

    public string? AboutMe { get; private set; }

    public string? Contact { get; private set; }

    public bool IsVisible { get; private set; }

    public IReadOnlyCollection<GoalCategory> Goals =>
        _goals.AsReadOnly();

    public IReadOnlyCollection<Interest> Interests =>
        _interests.AsReadOnly();

    public IReadOnlyCollection<Skill> Skills =>
        _skills.AsReadOnly();

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        Course.HasValue &&
        _goals.Count > 0 &&
        _interests.Count > 0;

    private UserProfile()
    {
    }

    public UserProfile(Guid id, Guid userId)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(
                "profile.user_id.empty",
                "User identifier cannot be empty.");
        }

        UserId = userId;
        IsVisible = true;
    }

    public void UpdateName(
        string? firstName,
        string? lastName)
    {
        FirstName = NormalizeOptional(firstName);
        LastName = NormalizeOptional(lastName);
    }

    public void SetFaculty(Guid? facultyId)
    {
        if (facultyId == Guid.Empty)
        {
            throw new DomainException(
                "profile.faculty.invalid",
                "Faculty identifier cannot be empty.");
        }

        FacultyId = facultyId;
    }

    public void SetCourse(int? course)
    {
        if (course.HasValue && course.Value <= 0)
        {
            throw new DomainException(
                "profile.course.invalid",
                "Course must be a positive number.");
        }

        Course = course;
    }

    public void UpdatePhoto(string? photoObjectKey)
    {
        PhotoObjectKey = NormalizeOptional(photoObjectKey);
    }

    public void UpdateAboutMe(string? aboutMe)
    {
        var normalized = NormalizeOptional(aboutMe);

        if (normalized is not null &&
            normalized.Length > MaxAboutMeLength)
        {
            throw new DomainException(
                "profile.about_me.too_long",
                $"About me cannot exceed {MaxAboutMeLength} characters.");
        }

        AboutMe = normalized;
    }

    public void UpdateContact(string? contact)
    {
        Contact = NormalizeOptional(contact);
    }

    public void SetVisibility(bool isVisible)
    {
        IsVisible = isVisible;
    }

    public void AddGoal(GoalCategory goal)
    {
        ValidateGoal(goal);

        if (_goals.Contains(goal))
        {
            throw new DomainException(
                "profile.goal.duplicate",
                "Goal is already selected.");
        }

        _goals.Add(goal);
    }

    public bool RemoveGoal(GoalCategory goal)
    {
        ValidateGoal(goal);

        return _goals.Remove(goal);
    }

    public void SetGoals(IEnumerable<GoalCategory> goals)
    {
        if (goals is null)
        {
            throw new DomainException(
                "profile.goals.null",
                "Goals collection cannot be null.");
        }

        var newGoals = goals.ToList();

        foreach (var goal in newGoals)
        {
            ValidateGoal(goal);
        }

        if (newGoals.Count != newGoals.Distinct().Count())
        {
            throw new DomainException(
                "profile.goal.duplicate",
                "Goals collection contains duplicates.");
        }

        _goals.Clear();
        _goals.AddRange(newGoals);
    }

    public void AddInterest(Interest interest)
    {
        if (interest is null)
        {
            throw new DomainException(
                "profile.interest.null",
                "Interest cannot be null.");
        }

        if (_interests.Any(x =>
            x.Id == interest.Id ||
            x.NormalizedName == interest.NormalizedName))
        {
            throw new DomainException(
                "profile.interest.duplicate",
                "Interest is already selected.");
        }

        if (_interests.Count >= MaxInterestsCount)
        {
            throw new DomainException(
                "profile.interests.limit_exceeded",
                $"Profile cannot have more than {MaxInterestsCount} interests.");
        }

        _interests.Add(interest);
    }

    public bool RemoveInterest(Guid interestId)
    {
        if (interestId == Guid.Empty)
        {
            throw new DomainException(
                "profile.interest_id.empty",
                "Interest identifier cannot be empty.");
        }

        var interest = _interests.FirstOrDefault(
            x => x.Id == interestId);

        return interest is not null &&
               _interests.Remove(interest);
    }

    public void AddSkill(Skill skill)
    {
        if (skill is null)
        {
            throw new DomainException(
                "profile.skill.null",
                "Skill cannot be null.");
        }

        if (_skills.Any(x =>
            x.Id == skill.Id ||
            x.NormalizedName == skill.NormalizedName))
        {
            throw new DomainException(
                "profile.skill.duplicate",
                "Skill is already selected.");
        }

        if (_skills.Count >= MaxSkillsCount)
        {
            throw new DomainException(
                "profile.skills.limit_exceeded",
                $"Profile cannot have more than {MaxSkillsCount} skills.");
        }

        _skills.Add(skill);
    }

    public bool RemoveSkill(Guid skillId)
    {
        if (skillId == Guid.Empty)
        {
            throw new DomainException(
                "profile.skill_id.empty",
                "Skill identifier cannot be empty.");
        }

        var skill = _skills.FirstOrDefault(
            x => x.Id == skillId);

        return skill is not null &&
               _skills.Remove(skill);
    }

    private static void ValidateGoal(GoalCategory goal)
    {
        if (!Enum.IsDefined(goal))
        {
            throw new DomainException(
                "profile.goal.invalid",
                "Unknown goal category.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}