namespace ITMO.Buddy.Application.Contracts.Common;

/// <summary>
/// Единый публичный контракт ошибки.
/// </summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    string Category,
    string? Details = null)
{
    public IReadOnlyList<ValidationError> ValidationErrors
    {
        get;
        init;
    } = Array.Empty<ValidationError>();
}