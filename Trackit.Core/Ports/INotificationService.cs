using System.Threading;
using System.Threading.Tasks;

namespace Trackit.Core.Ports
{
    // Service for sending email notifications.
    public interface INotificationService : IEmailSender
    {
        // Sends a notification email about a work order that is due soon.
        Task SendWorkOrderDueNotificationAsync(string userEmail, string workOrderSummary, DateTimeOffset dueDate, CancellationToken ct = default);

        // Sends a two-factor authentication verification code email.
        Task Send2FAVerificationCodeAsync(string userEmail, string verificationCode, CancellationToken ct = default);
    }
}
