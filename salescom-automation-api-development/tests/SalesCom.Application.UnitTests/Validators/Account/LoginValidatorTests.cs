namespace SalesCom.Application.UnitTests.Validators.Account;

using SalesCom.Application.Commands.Account.Login;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator _sut = new();

    [Fact]
    public void Valid_command_passes()
    {
        var result = _sut.Validate(new LoginCommand("alice", "secret"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Empty_username_fails(string? username)
    {
        var result = _sut.Validate(new LoginCommand(username!, "secret"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Username));
    }

    [Fact]
    public void Username_over_128_chars_fails()
    {
        var result = _sut.Validate(new LoginCommand(new string('a', 129), "secret"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Username));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_password_fails(string? password)
    {
        var result = _sut.Validate(new LoginCommand("alice", password!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Fact]
    public void Password_over_256_chars_fails()
    {
        var result = _sut.Validate(new LoginCommand("alice", new string('p', 257)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginCommand.Password));
    }
}
