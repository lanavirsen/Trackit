using OtpNet;
using System.Text;

namespace Trackit.Core.Services
{
    public sealed class TotpService
    {
        // Generate a new Base32-encoded secret (160 bits)
        public string GenerateSecret()
        {
            var key = KeyGeneration.GenerateRandomKey(20); // 160-bit
            return Base32Encoding.ToString(key);
        }

        // Create the standard otpauth URI used by authenticator apps
        public string BuildUri(string issuer, string accountName, string base32Secret)
        {
            return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
                   $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30&algorithm=SHA1";
        }

        // Verify a 6-digit code from the user
        public bool VerifyCode(string base32Secret, string code, int allowedDriftSteps = 1)
        {
            var bytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: allowedDriftSteps, future: allowedDriftSteps));
        }
    }
}
