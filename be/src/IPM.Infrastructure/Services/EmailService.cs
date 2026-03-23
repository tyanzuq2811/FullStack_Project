using IPM.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace IPM.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_configuration["Email:From"]));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        var builder = new BodyBuilder();
        if (isHtml)
            builder.HtmlBody = body;
        else
            builder.TextBody = body;

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _configuration["Email:SmtpHost"],
            int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
            SecureSocketOptions.StartTls,
            cancellationToken);

        await smtp.AuthenticateAsync(
            _configuration["Email:Username"],
            _configuration["Email:Password"],
            cancellationToken);

        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendBudgetAlertAsync(string to, string projectName, decimal actualCost, decimal estimatedCost, CancellationToken cancellationToken = default)
    {
        var subject = $"[CẢNH BÁO] Dự án {projectName} - Vượt ngân sách";
        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2 style='color: #e74c3c;'>Cảnh báo vượt ngân sách</h2>
            <p>Dự án <strong>{projectName}</strong> đã phát sinh chi phí vượt ngân sách dự kiến:</p>
            <table style='border-collapse: collapse; margin: 20px 0;'>
                <tr>
                    <td style='padding: 10px; border: 1px solid #ddd;'><strong>Ngân sách dự kiến:</strong></td>
                    <td style='padding: 10px; border: 1px solid #ddd;'>{estimatedCost:N0} VNĐ</td>
                </tr>
                <tr>
                    <td style='padding: 10px; border: 1px solid #ddd;'><strong>Chi phí thực tế:</strong></td>
                    <td style='padding: 10px; border: 1px solid #ddd; color: #e74c3c;'>{actualCost:N0} VNĐ</td>
                </tr>
                <tr>
                    <td style='padding: 10px; border: 1px solid #ddd;'><strong>Chênh lệch:</strong></td>
                    <td style='padding: 10px; border: 1px solid #ddd; color: #e74c3c;'>{(actualCost - estimatedCost):N0} VNĐ</td>
                </tr>
            </table>
            <p>Vui lòng kiểm tra và xác nhận chi phí phát sinh này.</p>
            <p style='color: #888;'>Email này được gửi tự động từ hệ thống IPM Pro.</p>
        </body>
        </html>";

        await SendEmailAsync(to, subject, body, true, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string to, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "Đặt lại mật khẩu - IPM Pro";
        var body = $@"
        <html>
        <body style='font-family: Arial, sans-serif;'>
            <h2>Yêu cầu đặt lại mật khẩu</h2>
            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
            <p>Nhấn vào nút bên dưới để đặt lại mật khẩu:</p>
            <a href='{resetLink}' style='display: inline-block; padding: 12px 24px; background-color: #3498db; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0;'>Đặt lại mật khẩu</a>
            <p style='color: #888;'>Link này sẽ hết hạn sau 15 phút.</p>
            <p style='color: #888;'>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
        </body>
        </html>";

        await SendEmailAsync(to, subject, body, true, cancellationToken);
    }
}
