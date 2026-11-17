namespace ITMO.Buddy.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; }

    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(
                "entity.id.empty",
                "Entity identifier cannot be empty.");
        }

        Id = id;
    }
}