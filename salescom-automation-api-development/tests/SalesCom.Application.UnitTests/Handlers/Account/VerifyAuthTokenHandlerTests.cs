namespace SalesCom.Application.UnitTests.Handlers.Account;

using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class VerifyAuthTokenHandlerTests
{
    private readonly ICentralLoginClient _central = Substitute.For<ICentralLoginClient>();
    private readonly IAuthSessionService _authSession = Substitute.For<IAuthSessionService>();
    private readonly ILoginLogger _logger = Substitute.For<ILoginLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly AuthSession _session = new("access.jwe", DateTimeOffset.UtcNow.AddMinutes(30), [1], "Test User", "testuser");

    public VerifyAuthTokenHandlerTests()
    {
        _authSession.IssueAsync(Arg.Any<CentralUserInfo>(), Arg.Any<CancellationToken>()).Returns(_session);
    }

    private VerifyAuthTokenHandler CreateSut() => new(_central, _authSession, _logger, _unitOfWork);

    private static CentralUserInfo UserInfo(bool isLocked = false, bool isActive = true) => new(
        4658, "marahaman", "Md Mahabur Rahaman", "m@example.com", "01939900223",
        IsInternal: true, IsLocked: isLocked, IsActive: isActive, CenterId: 99999, Department: "B2C", UserGroupId: 29);

    private void CentralReturns(CentralLoginResult result) =>
        _central.VerifyAuthTokenAsync("auth-token", Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task InvalidToken_ReturnsUnauthorized()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Rejected, null, null, "Token expired"));

        var result = await CreateSut().HandleAsync(new VerifyAuthTokenCommand("auth-token"), CancellationToken.None);

        result.Error.Code.Should().Be("User.AuthTokenInvalid");
        await _logger.Received(1).LogAsync(string.Empty, string.Empty, LoginStatus.Failed, "OTP VERIFICATION FAILED", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unavailable_ReturnsUnexpected()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Unavailable, null, null, null));

        var result = await CreateSut().HandleAsync(new VerifyAuthTokenCommand("auth-token"), CancellationToken.None);

        result.Error.Code.Should().Be("CentralLogin.Unavailable");
        await _logger.Received(1).LogAsync(string.Empty, string.Empty, LoginStatus.Failed, "CENTRAL LOGIN UNAVAILABLE", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockedUser_ReturnsLocked()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Success, UserInfo(isLocked: true), null, null));

        var result = await CreateSut().HandleAsync(new VerifyAuthTokenCommand("auth-token"), CancellationToken.None);

        result.Error.Should().Be(UserErrors.Locked);
        await _logger.Received(1).LogAsync("marahaman", "Md Mahabur Rahaman", LoginStatus.Failed, "USER LOCKED", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Success_IssuesSession_AndLogsOtpLoginSuccess()
    {
        CentralReturns(new CentralLoginResult(CentralLoginStatus.Success, UserInfo(), null, null));

        var result = await CreateSut().HandleAsync(new VerifyAuthTokenCommand("auth-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_session);
        await _authSession.Received(1).IssueAsync(
            Arg.Is<CentralUserInfo>(u => u.UserId == 4658), Arg.Any<CancellationToken>());
        await _logger.Received(1).LogAsync("marahaman", "Md Mahabur Rahaman", LoginStatus.Success, "OTP LOGIN SUCCESS", Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthToken_IsTrimmed()
    {
        _central.VerifyAuthTokenAsync("auth-token", Arg.Any<CancellationToken>())
            .Returns(new CentralLoginResult(CentralLoginStatus.Rejected, null, null, null));

        await CreateSut().HandleAsync(new VerifyAuthTokenCommand("  auth-token  "), CancellationToken.None);

        await _central.Received(1).VerifyAuthTokenAsync("auth-token", Arg.Any<CancellationToken>());
    }
}
