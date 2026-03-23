using IPM.Application.Contracts.SignalR;

namespace IPM.Application.Services.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(string userId, NotificationDto notification);
    Task SendTaskStatusChangedAsync(string projectId, TaskStatusChangedDto data);
    Task SendBookingStatusChangedAsync(BookingStatusChangedDto data);
    Task SendWalletBalanceChangedAsync(string userId, WalletBalanceChangedDto data);
    Task SendAiAnalysisCompletedAsync(string userId, AiAnalysisCompletedDto data);

    // Group notification methods
    Task SendNotificationToGroupAsync(string groupName, NotificationDto notification);
    Task SendBookingStatusChangedToGroupAsync(string groupName, BookingStatusChangedDto data);
    Task SendTaskStatusChangedToGroupAsync(string groupName, TaskStatusChangedDto data);
    Task SendWalletBalanceChangedToGroupAsync(string groupName, WalletBalanceChangedDto data);
    Task SendAiAnalysisCompletedToGroupAsync(string groupName, AiAnalysisCompletedDto data);

    // Group management methods
    Task AddToProjectGroupAsync(string connectionId, string projectId);
    Task RemoveFromProjectGroupAsync(string connectionId, string projectId);
    Task AddToUserGroupAsync(string connectionId, string userId);
    Task RemoveFromUserGroupAsync(string connectionId, string userId);
}