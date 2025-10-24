using Trackit.Core.Services;
using Xunit;

namespace Trackit.Tests
{
    public class TotpServiceTests
    {
        [Fact]
        public void GeneratedCodeShouldValidate()
        {
            var service = new TotpService();
            var secret = service.GenerateSecret();
            var valid = service.VerifyCode(secret, new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret)).ComputeTotp());
            Assert.True(valid);
        }
    }
}
