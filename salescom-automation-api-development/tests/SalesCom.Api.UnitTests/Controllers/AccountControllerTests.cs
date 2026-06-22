namespace SalesCom.Api.UnitTests.Controllers;

using Microsoft.AspNetCore.Mvc;
using SalesCom.Api.Controllers;
using SalesCom.Application.Commands.Account.Login;
using SalesCom.Application.Commands.Account.VerifyAuthToken;
using SalesCom.Application.Common;
using SalesCom.Application.Messaging;
using SalesCom.Application.Queries.Account.GetMe;
using SalesCom.Domain.Common;
using SalesCom.Domain.Errors;

public sealed class AccountControllerTests
{
    private readonly ICommandDispatcher _commands = Substitute.For<ICommandDispatcher>();
    private readonly IQueryDispatcher _queries = Substitute.For<IQueryDispatcher>();

    private AccountController CreateSut() => new(_commands, _queries);

    private static AuthSession Session() =>
        new("access.jwe", DateTimeOffset.UtcNow.AddMinutes(30), [1, 2], "Test User", "testuser");

    [Fact]
    public async Task Login_normal_returns_200_with_session()
    {
        _commands.DispatchAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(LoginResponse.Normal(Session())));

        var actionResult = await CreateSut().LoginAsync(new LoginCommand("alice", "secret"), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        var body = objectResult.Value.Should().BeOfType<ApiResponse<LoginResponse>>().Subject;
        body.Data!.AuthType.Should().Be("Normal");
        body.Data.Session!.AccessToken.Should().Be("access.jwe");
        body.Data.Session.Rights.Should().Equal(1, 2);
        body.Data.RedirectUrl.Should().BeNull();
    }

    [Fact]
    public async Task Login_sso_returns_200_with_redirect()
    {
        _commands.DispatchAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(LoginResponse.Sso("https://otp/x")));

        var body = (await CreateSut().LoginAsync(new LoginCommand("alice", "secret"), CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject
            .Value.Should().BeOfType<ApiResponse<LoginResponse>>().Subject;
        body.Data!.AuthType.Should().Be("SSO");
        body.Data.RedirectUrl.Should().Be("https://otp/x");
        body.Data.Session.Should().BeNull();
    }

    [Fact]
    public async Task Login_invalid_returns_401()
    {
        _commands.DispatchAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LoginResponse>(UserErrors.InvalidCredentials));

        var objectResult = (await CreateSut().LoginAsync(new LoginCommand("a", "b"), CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(401);
        objectResult.Value.Should().BeOfType<ApiResponse<LoginResponse>>().Subject.ErrorCode.Should().Be("User.InvalidCredentials");
    }

    [Fact]
    public async Task Login_locked_returns_403()
    {
        _commands.DispatchAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LoginResponse>(UserErrors.Locked));

        (await CreateSut().LoginAsync(new LoginCommand("a", "b"), CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task VerifyAuthToken_success_returns_200_with_session()
    {
        _commands.DispatchAsync(Arg.Any<VerifyAuthTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Session()));

        var body = (await CreateSut().VerifyAuthTokenAsync(new VerifyAuthTokenCommand("t"), CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject
            .Value.Should().BeOfType<ApiResponse<AuthSession>>().Subject;
        body.Data!.AccessToken.Should().Be("access.jwe");
    }

    [Fact]
    public async Task Me_success_returns_200_with_profile()
    {
        var me = new MeResponse("102941", "alice", "Alice Rahman", "a@x.com", "01900000000", "CCD", [1001, 1002]);
        _queries.DispatchAsync(Arg.Any<GetMeQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(me));

        var body = (await CreateSut().MeAsync(CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject
            .Value.Should().BeOfType<ApiResponse<MeResponse>>().Subject;
        body.Data!.UserName.Should().Be("alice");
        body.Data.UserId.Should().Be("102941");
        body.Data.Rights.Should().Equal(1001, 1002);
    }

    [Fact]
    public async Task Me_missing_returns_404()
    {
        _queries.DispatchAsync(Arg.Any<GetMeQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MeResponse>(UserErrors.NotFound));

        (await CreateSut().MeAsync(CancellationToken.None))
            .Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(404);
    }
}
