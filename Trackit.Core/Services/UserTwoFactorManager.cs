using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Core.Services
{
    public sealed class UserTwoFactorManager
    {
        private readonly IUserRepository _repo;
        private readonly TotpService _totp;

        public UserTwoFactorManager(IUserRepository repo, TotpService totp)
        {
            _repo = repo;
            _totp = totp;
        }

        public async Task<(string secret, string uri)> EnableAsync(User user, string issuer, CancellationToken ct = default)
        {
            var secret = _totp.GenerateSecret();
            user.TotpSecret = secret;
            user.TwoFactorEnabled = true;

            await _repo.UpdateAsync(user, ct);
            return (secret, _totp.BuildUri(issuer, user.Username, secret));
        }

        public async Task DisableAsync(User user, CancellationToken ct = default)
        {
            user.TotpSecret = null;
            user.TwoFactorEnabled = false;
            await _repo.UpdateAsync(user, ct);
        }
    }
}
