namespace Trackit.Core.Ports
{
    // Service for sending email notifications
    public interface INotificationService
    {
        // Sends an email notification
        Task SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null, CancellationToken ct = default);

        // Sends a work order due notification
        Task SendWorkOrderDueNotificationAsync(string userEmail, string workOrderSummary, DateTimeOffset dueDate, CancellationToken ct = default);

        // Sends a 2FA verification code email
        Task Send2FAVerificationCodeAsync(string userEmail, string verificationCode, CancellationToken ct = default);
    }
}
