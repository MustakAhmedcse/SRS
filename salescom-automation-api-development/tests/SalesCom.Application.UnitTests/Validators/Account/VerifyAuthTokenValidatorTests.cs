namespace SalesCom.Application.UnitTests.Validators.Account;

using SalesCom.Application.Commands.Account.VerifyAuthToken;

public sealed class VerifyAuthTokenValidatorTests
{
    private readonly VerifyAuthTokenValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_auth_token_fails(string token)
    {
        var result = await _validator.ValidateAsync(new VerifyAuthTokenCommand(token));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Auth_token_longer_than_2048_chars_fails()
    {
        var result = await _validator.ValidateAsync(new VerifyAuthTokenCommand(new string('x', 2049)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Valid_auth_token_passes()
    {
        var result = await _validator.ValidateAsync(new VerifyAuthTokenCommand("aVlxU2ZTOUZwQVNMNit5d1h1QTJibXE3WXgz"));

        result.IsValid.Should().BeTrue();
    }
}
