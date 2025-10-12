using System.Threading.Tasks;
using FluentAssertions;
using Trackit.Core.Auth;
using Trackit.Core.Services;
using Trackit.Data.Repositories;
using Xunit;

namespace Trackit.Tests
{
    public class UserServiceTests
    {
        private static UserService NewSvc() =>
            new(new InMemoryUserRepository(), new PasswordHasher(), () => new System.DateTimeOffset(2025, 10, 10, 0, 0, 0, System.TimeSpan.Zero));

        [Fact]
        public async Task Register_then_login_succeeds()
        {
            var svc = NewSvc();
            var id = await svc.RegisterAsync("Lana", "lana@example.com", "P@ssw0rd!");
            id.Should().BeGreaterThan(0);

            var res = await svc.LoginAsync("lana", "P@ssw0rd!");
            res.IsSuccess.Should().BeTrue();
            res.User!.Username.Should().Be("lana"); // normalized
            res.User.Email.Should().Be("lana@example.com");
        }

        [Fact]
        public async Task Duplicate_username_is_rejected()
        {
            var svc = NewSvc();
            await svc.RegisterAsync("lana", null, "x1!x1!x1!");

            var act = async () => await svc.RegisterAsync("LANA", null, "y2@y2@y2@");
            await act.Should().ThrowAsync<System.InvalidOperationException>();
        }

        [Fact]
        public async Task Whitespace_password_is_rejected()
        {
            var svc = NewSvc();
            var act = async () => await svc.RegisterAsync("lana", null, "   ");
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("password");
        }

        [Fact]
        public async Task Wrong_password_fails()
        {
            var svc = NewSvc();
            await svc.RegisterAsync("lana", null, "P@ssw0rd!");

            var res = await svc.LoginAsync("lana", "wrong");
            res.IsSuccess.Should().BeFalse();
            res.Error.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Missing_credentials_fails_fast()
        {
            var svc = NewSvc();
            (await svc.LoginAsync("", "x")).IsSuccess.Should().BeFalse();
            (await svc.LoginAsync("lana", "")).IsSuccess.Should().BeFalse();
        }
    }
}
