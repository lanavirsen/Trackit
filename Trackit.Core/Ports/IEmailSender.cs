using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trackit.Core.Ports
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null, CancellationToken ct = default);
    }
}
