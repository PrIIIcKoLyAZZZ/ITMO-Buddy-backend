using System.Text;

namespace ITMO.Buddy.Domain.Common;

internal static class ReferenceName
{
    public static (string Name, string NormalizedName) Normalize(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "reference.name.empty",
                "Reference name cannot be empty.");
        }

        var normalizedUnicode = name.Normalize(
            NormalizationForm.FormKC);

        var canonicalName = string.Join(
            " ",
            normalizedUnicode.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        var normalizedName = canonicalName.ToUpperInvariant();

        return (canonicalName, normalizedName);
    }
}