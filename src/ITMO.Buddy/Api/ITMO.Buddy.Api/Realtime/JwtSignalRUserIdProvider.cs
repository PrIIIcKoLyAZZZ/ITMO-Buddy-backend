using System.Security.Claims;
using ITMO.Buddy.Api.Authentication;
using Microsoft.AspNetCore.SignalR;

namespace ITMO.Buddy.Api.Realtime;

internal sealed class JwtSignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        HttpCurrentUser.ReadIdentity(connection.User?.Identity as ClaimsIdentity)?.UserId.ToString("D");
}
