using System.Security.Claims;
using System.Text.Encodings.Web;
using GuessingGame.API.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GuessingGame.API.Authentication;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string HeaderName = "X-API-Key";

    private readonly AppDbContext _context;

    public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext context) : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        string rawApiKey = headerValues.ToString().Trim();

        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            Logger.LogWarning("Empty API key submitted from IP address {RemoteIpAddress}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("The API key is empty.");
        }

        string apiKeyHash = SecurityTokenHelper.HashToken(rawApiKey);

        var user = await _context.Users
            .AsNoTracking()
            .Include(user => user.Player)
            .FirstOrDefaultAsync(user => user.ApiKeyHash == apiKeyHash);

        if (user is null)
        {
            Logger.LogWarning("Invalid API key submitted from IP address {RemoteIpAddress}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("The API key is invalid.");
        }

        List<Claim> claims = UserClaimsFactory.Create(user, "ApiKey");

        var identity = new ClaimsIdentity(claims, AuthSchemes.ApiKey);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(principal, AuthSchemes.ApiKey);

        return AuthenticateResult.Success(ticket);
    }
}