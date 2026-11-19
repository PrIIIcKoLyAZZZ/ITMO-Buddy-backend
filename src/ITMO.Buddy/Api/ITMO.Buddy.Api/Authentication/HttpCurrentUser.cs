using System.Security.Claims;
using ITMO.Buddy.Application.Abstractions.Identity;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Api.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    internal const string SubjectClaimType = "sub";
    internal const string RoleClaimType = "role";

    public bool IsAuthenticated => ReadIdentity().HasValue;
    public Guid? UserId => ReadIdentity()?.UserId;
    public UserRole? Role => ReadIdentity()?.Role;

    private (Guid UserId, UserRole Role)? ReadIdentity() =>
        ReadIdentity(accessor.HttpContext?.User.Identity as ClaimsIdentity);

    internal static (Guid UserId, UserRole Role)? ReadIdentity(ClaimsIdentity? identity)
    {
        if (identity is not { IsAuthenticated: true })
        {
            return null;
        }

        var subjects = identity.FindAll(SubjectClaimType).ToArray();
        var roles = identity.FindAll(RoleClaimType).ToArray();
        if (subjects.Length != 1 || roles.Length != 1 ||
            !Guid.TryParse(subjects[0].Value, out var userId) || userId == Guid.Empty)
        {
            return null;
        }

        return roles[0].Value switch
        {
            nameof(UserRole.Student) => (userId, UserRole.Student),
            nameof(UserRole.Administrator) => (userId, UserRole.Administrator),
            _ => null
        };
    }
}
