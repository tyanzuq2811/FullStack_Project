namespace IPM.Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task SendBudgetAlertAsync(string to, string projectName, decimal actualCost, decimal estimatedCost, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string to, string resetLink, CancellationToken cancellationToken = default);
}
