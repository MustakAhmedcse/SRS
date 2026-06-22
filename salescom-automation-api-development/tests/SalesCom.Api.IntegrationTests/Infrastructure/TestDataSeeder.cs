namespace SalesCom.Api.IntegrationTests.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalesCom.Application.Authorization;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Seeds the test user (matching <see cref="TestAuthHandler.TestUserId"/>) and grants it the
/// data-source rights, so <c>[HasRight]</c>-protected endpoints and <c>GET /me</c> succeed under the
/// stub auth scheme. Goes through the unit of work, like the rest of the app. Registered after the
/// migration initializer so the schema already exists.
/// </summary>
internal sealed class TestDataSeeder(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var exists = await unitOfWork.Repository<User>()
            .AnyAsync(u => u.UserId == TestAuthHandler.TestUserId, cancellationToken);
        if (exists)
        {
            return;
        }

        var user = await unitOfWork.Repository<User>().AddAsync(
            new User
            {
                UserId = TestAuthHandler.TestUserId,
                UserName = "test",
                FullName = "Test User",
                Email = "test@example.com",
                MobileNo = "0000000000",
                Department = "QA",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test-seed",
            },
            cancellationToken);

        await unitOfWork.Repository<UserRight>().AddRangeAsync(
            [
                new UserRight { UserId = int.Parse(user.UserId), RightsCode = Rights.DataSources.View },
                new UserRight { UserId = int.Parse(user.UserId), RightsCode = Rights.DataSources.Manage },
            ],
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
