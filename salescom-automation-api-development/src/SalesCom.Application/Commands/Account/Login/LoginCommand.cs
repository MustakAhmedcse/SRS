namespace SalesCom.Application.Commands.Account.Login;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

public sealed record LoginCommand(string Username, string Password, bool RememberMe = false) : ICommand<Result<LoginResponse>>;
