using System.Net.Http;
using System.Text;
using System.Text.Json;
using Trackit.Core.Ports;

namespace Trackit.Cli.Infrastructure
{
    public sealed class ResendEmailSender : IEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public ResendEmailSender(HttpClient httpClient, string apiKey, string fromEmail)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _fromEmail = fromEmail;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string htmlContent,
            string? textContent = null,
            CancellationToken ct = default)
        {
            var emailData = new
            {
                from = _fromEmail,
                to = new[] { to },
                subject,
                html = htmlContent
            };

            var json = JsonSerializer.Serialize(emailData);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("https://api.resend.com/emails", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Resend email failed: {response.StatusCode} - {error}");
            }
        }
    }
}
