namespace IPM.Application.Contracts.SignalR;

public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task TaskStatusChanged(TaskStatusChangedDto data);
    Task BookingStatusChanged(BookingStatusChangedDto data);
    Task WalletBalanceChanged(WalletBalanceChangedDto data);
    Task AiAnalysisCompleted(AiAnalysisCompletedDto data);
}

// DTOs for SignalR
public record NotificationDto(string Title, string Message, string Type, DateTime Timestamp);

public record TaskStatusChangedDto(long TaskId, string TaskName, int OldStatus, int NewStatus, int ProgressPct, Guid ProjectId);

public record BookingStatusChangedDto(Guid BookingId, int ResourceId, DateTime StartTime, DateTime EndTime, int Status);

public record WalletBalanceChangedDto(Guid MemberId, decimal NewBalance, decimal ChangeAmount, string TransactionType);

public record AiAnalysisCompletedDto(Guid MediaId, long TaskId, int ProgressPct, List<string> Anomalies, bool HasAnomalies);
