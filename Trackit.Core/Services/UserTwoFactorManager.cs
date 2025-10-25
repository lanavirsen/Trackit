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

        // Persist after verification
        public async Task EnableAsync(int userId, string base32Secret, CancellationToken ct = default)
        {
            var user = await _repo.GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("User not found");
            user.TotpSecret = base32Secret;
            user.TwoFactorEnabled = true;
            await _repo.UpdateAsync(user, ct);
        }

        public async Task DisableAsync(int userId, CancellationToken ct = default)
        {
            var user = await _repo.GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("User not found");
            user.TotpSecret = null;
            user.TwoFactorEnabled = false;
            await _repo.UpdateAsync(user, ct);
        }
    }
}
