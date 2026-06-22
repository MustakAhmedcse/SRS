namespace SalesCom.Application.Queries.Account.GetMe;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

public sealed record GetMeQuery : IQuery<Result<MeResponse>>;
