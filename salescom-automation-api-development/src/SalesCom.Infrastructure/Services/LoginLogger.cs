namespace SalesCom.Infrastructure.Services;

using System.Globalization;
using Microsoft.Extensions.Logging;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.Auditing;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Interfaces;

/// <summary>
/// Stages a login-attempt row for the <c>login</c> table AND emits a structured log entry. The DB
/// row is the durable audit record; the log line surfaces auth activity in real time through
/// Serilog/Loki. The row is staged through the <see cref="IUnitOfWork"/> and commits atomically with
/// the rest of the login work when the caller calls <see cref="IUnitOfWork.Commit"/>.
/// </summary>
internal sealed class LoginLogger(
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<LoginLogger> logger) : ILoginLogger
{
    public async Task LogAsync(string userName, string fullName, LoginStatus status, string remarks, CancellationToken cancellationToken)
    {
        var entry = new LoginLog
        {
            UserName = userName,
            FullName = fullName,
            LoginTime = clock.UtcNow,
            LoginStatus = status,
            Remarks = remarks,
        };

        await unitOfWork.Repository<LoginLog>().AddAsync(entry, cancellationToken);

        var level = status == LoginStatus.Failed ? LogLevel.Warning : LogLevel.Information;
        logger.Log(level, "Login attempt: status={Status} user={User} remarks={Remarks}", status, userName, remarks);
    }
}
