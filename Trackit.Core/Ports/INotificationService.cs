using System.Threading;
using System.Threading.Tasks;

namespace Trackit.Core.Ports
{
    // Service for sending email notifications
    public interface INotificationService : IEmailSender
    {
        Task SendWorkOrderDueNotificationAsync(string userEmail, string workOrderSummary, DateTimeOffset dueDate, CancellationToken ct = default);
        Task Send2FAVerificationCodeAsync(string userEmail, string verificationCode, CancellationToken ct = default);
    }
}
