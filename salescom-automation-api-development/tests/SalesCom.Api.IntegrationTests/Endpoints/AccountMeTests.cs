namespace SalesCom.Api.IntegrationTests.Endpoints;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SalesCom.Api.IntegrationTests.Infrastructure;

[Collection(SalesComCollection.Name)]
public sealed class AccountMeTests(SalesComFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Me_returns_profile_from_the_authenticated_claims()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await _client.GetAsync("/api/account/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto>();
        body!.Success.Should().BeTrue();
    }
}
