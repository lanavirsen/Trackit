using Trackit.Core.Services;
using Xunit;

namespace Trackit.Tests
{
    public class TotpServiceTests
    {
        [Fact]
        public void GeneratedCode_ShouldValidate_WithAllowedDrift()
        {
            var svc = new TotpService();
            var secret = svc.GenerateSecret();

            // Freeze time to avoid boundary flakiness
            var now = DateTime.UtcNow;
            var codeNow = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret)).ComputeTotp(now);

            Assert.True(svc.VerifyCode(secret, codeNow, allowedDriftSteps: 1));

            // Negative case: wrong code must fail
            var bogus = codeNow == "000000" ? "123456" : "000000";
            Assert.False(svc.VerifyCode(secret, bogus, allowedDriftSteps: 1));
        }
    }
}
