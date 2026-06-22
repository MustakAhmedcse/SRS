namespace SalesCom.Application.Commands.Account.VerifyAuthToken;

using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

/// <summary>
/// Exchanges the auth token the central OTP page appended to the redirect-back URL for this
/// application's own access token. Sent by the frontend after a successful 2FA verification.
/// </summary>
public sealed record VerifyAuthTokenCommand(string AuthToken) : ICommand<Result<AuthSession>>;
