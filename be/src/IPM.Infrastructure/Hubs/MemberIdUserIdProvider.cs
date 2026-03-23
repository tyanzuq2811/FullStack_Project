using Microsoft.AspNetCore.SignalR;

namespace IPM.Infrastructure.Hubs;

public class MemberIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.Claims.FirstOrDefault(c => c.Type == "memberId")?.Value;
    }
}
