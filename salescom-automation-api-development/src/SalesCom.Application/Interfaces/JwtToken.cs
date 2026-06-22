namespace SalesCom.Application.Interfaces;

public sealed record JwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
