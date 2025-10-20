using System.Text;
using System.Text.Json;
using Trackit.Core.Ports;

namespace Trackit.Data.Services
{
    // Resend implementation of the notification service.
    public sealed class ResendNotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public ResendNotificationService(string apiKey, string fromEmail = "onboarding@resend.dev")
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _fromEmail = fromEmail;
            
            // Set up the API key in headers.
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        // Sends a generic email.
        public async Task SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null, CancellationToken ct = default)
        {
            // Prepare the email payload.
            var emailData = new
            {
                from = _fromEmail,
                to = new[] { to },
                subject = subject,
                html = htmlContent
            };

            var json = JsonSerializer.Serialize(emailData);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // `using` avoids memory pressure and socket leaks when the notifier runs repeatedly (for example, sending many due notifications).
            using var response = await _httpClient.PostAsync("https://api.resend.com/emails", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"Error: {errorContent}");
                throw new Exception($"Failed to send email: {response.StatusCode} - {errorContent}");
            }
            
            Console.WriteLine("Email sent successfully!");
        }

        public async Task SendWorkOrderDueNotificationAsync(string userEmail, string workOrderSummary, DateTimeOffset dueDate, CancellationToken ct = default)
        {
            var subject = "Work Order Due Soon - Trackit";
            var dueDateFormatted = dueDate.ToString("yyyy-MM-dd HH:mm");
            
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Work Order Due Soon</h2>
                        <p>Hello,</p>
                        <p>This is a reminder that you have a work order due soon:</p>
                        <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #007bff; margin: 20px 0;'>
                            <h3 style='margin-top: 0; color: #007bff;'>{workOrderSummary}</h3>
                            <p><strong>Due Date:</strong> {dueDateFormatted}</p>
                        </div>
                        <p>Please log into Trackit to view and manage your work orders.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                        <p style='color: #666; font-size: 12px;'>This is an automated notification from Trackit.</p>
                    </div>
                </body>
                </html>";

            var textContent = $@"
Work Order Due Soon - Trackit

Hello,

This is a reminder that you have a work order due soon:

Work Order: {workOrderSummary}
Due Date: {dueDateFormatted}

Please log into Trackit to view and manage your work orders.

This is an automated notification from Trackit.";

            await SendEmailAsync(userEmail, subject, htmlContent, textContent, ct);
        }

        public async Task Send2FAVerificationCodeAsync(string userEmail, string verificationCode, CancellationToken ct = default)
        {
            var subject = "Your Trackit Verification Code";
            
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Two-Factor Authentication</h2>
                        <p>Hello,</p>
                        <p>You requested a verification code for your Trackit account. Use the code below to complete your login:</p>
                        <div style='background-color: #f8f9fa; padding: 20px; text-align: center; margin: 20px 0; border: 2px solid #007bff; border-radius: 8px;'>
                            <h1 style='color: #007bff; font-size: 32px; letter-spacing: 4px; margin: 0;'>{verificationCode}</h1>
                        </div>
                        <p><strong>This code will expire in 10 minutes.</strong></p>
                        <p>If you didn't request this code, please ignore this email.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
                        <p style='color: #666; font-size: 12px;'>This is an automated message from Trackit.</p>
                    </div>
                </body>
                </html>";

            var textContent = $@"
Two-Factor Authentication - Trackit

Hello,

You requested a verification code for your Trackit account. Use the code below to complete your login:

Verification Code: {verificationCode}

This code will expire in 10 minutes.

If you didn't request this code, please ignore this email.

This is an automated message from Trackit.";

            await SendEmailAsync(userEmail, subject, htmlContent, textContent, ct);
        }
    }
}
