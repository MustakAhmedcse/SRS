namespace SalesCom.Api.IntegrationTests.Endpoints;

using System.Net;
using SalesCom.Api.IntegrationTests.Infrastructure;

[Collection(SalesComCollection.Name)]
public sealed class HealthEndpointsTests(SalesComFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_probe_returns_ok()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_probe_returns_ok()
    {
        var response = await _client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
