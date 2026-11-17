using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Users;

public sealed class Faculty : Entity
{
    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    private Faculty()
    {
    }

    public Faculty(Guid id, string name)
        : base(id)
    {
        var normalized = ReferenceName.Normalize(name);

        Name = normalized.Name;
        NormalizedName = normalized.NormalizedName;
    }
}