using System.Threading;
using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Core.Services
{
    public sealed class UserTwoFactorManager
    {
        private readonly IUserRepository _repo;

        public UserTwoFactorManager(IUserRepository repo)
        {
            _repo = repo;
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
