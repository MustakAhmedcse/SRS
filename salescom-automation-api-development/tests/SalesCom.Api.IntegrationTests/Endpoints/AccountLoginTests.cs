namespace SalesCom.Api.IntegrationTests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using SalesCom.Api.IntegrationTests.Infrastructure;

[Collection(SalesComCollection.Name)]
public sealed class AccountLoginTests(SalesComFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Empty_username_returns_400_validation_error()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login", new { username = "", password = "secret" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.Success.Should().BeFalse();
        body.ErrorCode.Should().StartWith("Validation.");
    }

    [Fact]
    public async Task Empty_password_returns_400_validation_error()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login", new { username = "alice", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_user_returns_401_invalid_credentials()
    {
        var response = await _client.PostAsJsonAsync("/api/account/login", new { username = "ghost-user-does-not-exist", password = "anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.Success.Should().BeFalse();
        body.ErrorCode.Should().Be("User.InvalidCredentials");
    }
}
