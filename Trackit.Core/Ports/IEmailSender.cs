using System.Threading;
using System.Threading.Tasks;

namespace Trackit.Core.Ports
{
    // Port for sending emails.
    public interface IEmailSender
    {
        // Sends an email asynchronously.
        Task SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null, CancellationToken ct = default);
    }
}
