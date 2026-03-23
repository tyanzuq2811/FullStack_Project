using IPM.Application.Services.Interfaces;
using IPM.Application.Contracts.SignalR;
using IPM.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IPM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public NotificationService(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(string userId, NotificationDto notification)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .ReceiveNotification(notification);
    }

    public async Task SendTaskStatusChangedAsync(string projectId, TaskStatusChangedDto data)
    {
        await _hubContext.Clients.Group($"project_{projectId}")
            .TaskStatusChanged(data);
    }

    public async Task SendBookingStatusChangedAsync(BookingStatusChangedDto data)
    {
        await _hubContext.Clients.All
            .BookingStatusChanged(data);
    }

    public async Task SendWalletBalanceChangedAsync(string userId, WalletBalanceChangedDto data)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .WalletBalanceChanged(data);
    }

    public async Task SendAiAnalysisCompletedAsync(string userId, AiAnalysisCompletedDto data)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .AiAnalysisCompleted(data);
    }

    // Group notification methods
    public async Task SendNotificationToGroupAsync(string groupName, NotificationDto notification)
    {
        await _hubContext.Clients.Group(groupName)
            .ReceiveNotification(notification);
    }

    public async Task SendBookingStatusChangedToGroupAsync(string groupName, BookingStatusChangedDto data)
    {
        await _hubContext.Clients.Group(groupName)
            .BookingStatusChanged(data);
    }

    public async Task SendTaskStatusChangedToGroupAsync(string groupName, TaskStatusChangedDto data)
    {
        await _hubContext.Clients.Group(groupName)
            .TaskStatusChanged(data);
    }

    public async Task SendWalletBalanceChangedToGroupAsync(string groupName, WalletBalanceChangedDto data)
    {
        await _hubContext.Clients.Group(groupName)
            .WalletBalanceChanged(data);
    }

    public async Task SendAiAnalysisCompletedToGroupAsync(string groupName, AiAnalysisCompletedDto data)
    {
        await _hubContext.Clients.Group(groupName)
            .AiAnalysisCompleted(data);
    }

    // Group management methods
    public async Task AddToProjectGroupAsync(string connectionId, string projectId)
    {
        await _hubContext.Groups.AddToGroupAsync(connectionId, $"project_{projectId}");
    }

    public async Task RemoveFromProjectGroupAsync(string connectionId, string projectId)
    {
        await _hubContext.Groups.RemoveFromGroupAsync(connectionId, $"project_{projectId}");
    }

    public async Task AddToUserGroupAsync(string connectionId, string userId)
    {
        await _hubContext.Groups.AddToGroupAsync(connectionId, $"user_{userId}");
    }

    public async Task RemoveFromUserGroupAsync(string connectionId, string userId)
    {
        await _hubContext.Groups.RemoveFromGroupAsync(connectionId, $"user_{userId}");
    }
}