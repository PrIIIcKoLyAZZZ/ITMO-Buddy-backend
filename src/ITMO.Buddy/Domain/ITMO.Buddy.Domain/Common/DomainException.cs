namespace ITMO.Buddy.Domain.Common;

public sealed class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Error code cannot be empty.",
                nameof(code));
        }

        Code = code;
    }
}