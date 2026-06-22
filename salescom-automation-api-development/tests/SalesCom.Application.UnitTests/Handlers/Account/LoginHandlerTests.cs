namespace SalesCom.Application.UnitTests.Handlers.Account;

using SalesCom.Application.Commands.Account.Login;
using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class LoginHandlerTests
{
    private readonly ICentralLoginClient _central = Substitute.For<ICentralLoginClient>();
    private readonly IAuthSessionService _authSession = Substitute.For<IAuthSessionService>();
    private readonly ILoginLogger _logger = Substitute.For<ILoginLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly AuthSession _session = new("access.jwe", DateTimeOffset.UtcNow.AddMinutes(30), [1, 2], "Test User", "testuser");

    public LoginHandlerTests()
    {
        _authSession.IssueAsync(Arg.Any<CentralUserInfo>(), Arg.Any<CancellationToken>()).Returns(_session);
    }

    private LoginHandler CreateSut() => new(_central, _authSession, _logger, _unitOfWork);

    private static CentralUserInfo UserInfo(int userId = 102941, bool isLocked = false, bool isActive = true) => new(
        userId, "alice", "Alice Rahman", "alice@example.com", "01900000000",
        IsInternal: false, IsLocked: isLocked, IsActive: isActive, CenterId: 1787, Department: "CCD", UserGroupId: 45);

    private void CentralReturns(CentralLoginResult result) =>
        _central.LoginAsync("alice", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task Rejected_ReturnsUnauthorized_AndLogsFailure()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Rejected, null, null, "Invalid credentials"));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "bad"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.InvalidCredentials");
        await _logger.Received(1).LogAsync("alice", string.Empty, LoginStatus.Failed, "Invalid credentials", Arg.Any<CancellationToken>());
        await _authSession.DidNotReceive().IssueAsync(Arg.Any<CentralUserInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unavailable_ReturnsUnexpected()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, null));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "x"), CancellationToken.None);

        result.Error.Code.Should().Be("CentralLogin.Unavailable");
        result.Error.Type.Should().Be(ErrorType.Unexpected);
        await _logger.Received(1).LogAsync("alice", string.Empty, LoginStatus.Failed, "CENTRAL LOGIN UNAVAILABLE", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SsoRedirect_ReturnsRedirect_NoSession()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.SsoRedirect, null, "https://otp/x", "OTP sent"));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthType.Should().Be("SSO");
        result.Value.RedirectUrl.Should().Be("https://otp/x");
        result.Value.Session.Should().BeNull();
        await _logger.Received(1).LogAsync("alice", string.Empty, LoginStatus.Success, "OTP CHALLENGE ISSUED", Arg.Any<CancellationToken>());
        await _authSession.DidNotReceive().IssueAsync(Arg.Any<CentralUserInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalUser_IssuesSession_AndLogsSuccess()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Success, UserInfo(), null, null));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "good"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthType.Should().Be("Normal");
        result.Value.Session.Should().Be(_session);
        result.Value.RedirectUrl.Should().BeNull();
        await _authSession.Received(1).IssueAsync(
            Arg.Is<CentralUserInfo>(u => u.UserId == 102941), Arg.Any<CancellationToken>());
        await _logger.Received(1).LogAsync("alice", "Alice Rahman", LoginStatus.Success, "LOGIN SUCCESS", Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockedExternalUser_ReturnsLocked_NoSession()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Success, UserInfo(isLocked: true), null, null));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "x"), CancellationToken.None);

        result.Error.Should().Be(UserErrors.Locked);
        await _logger.Received(1).LogAsync("alice", "Alice Rahman", LoginStatus.Failed, "USER LOCKED", Arg.Any<CancellationToken>());
        await _authSession.DidNotReceive().IssueAsync(Arg.Any<CentralUserInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InactiveExternalUser_ReturnsNotActive()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Success, UserInfo(isActive: false), null, null));

        var result = await CreateSut().HandleAsync(new LoginCommand("alice", "x"), CancellationToken.None);

        result.Error.Should().Be(UserErrors.NotActive);
    }
}
