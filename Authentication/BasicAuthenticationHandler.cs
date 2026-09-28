using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GuessingGame.API.Data;
using GuessingGame.API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GuessingGame.API.Authentication;

public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _context;

    private readonly IPasswordHasher<User> _passwordHasher;

    public BasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext context,
        IPasswordHasher<User> passwordHasher) : base(options, logger, encoder)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorizationHeader = Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out AuthenticationHeaderValue? header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!string.Equals(header.Scheme, AuthSchemes.Basic, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        if (string.IsNullOrWhiteSpace(header.Parameter))
        {
            return AuthenticateResult.Fail("Basic credentials are missing.");
        }

        string credentials;

        try
        {
            byte[] credentialBytes = Convert.FromBase64String(header.Parameter);

            credentials = Encoding.UTF8.GetString(credentialBytes);
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Basic credentials are not valid Base64.");
        }

        int separatorIndex = credentials.IndexOf(':');

        if (separatorIndex <= 0)
        {
            return AuthenticateResult.Fail("Basic credentials are invalid.");
        }

        string email = credentials[..separatorIndex].Trim().ToLowerInvariant();

        string password = credentials[(separatorIndex + 1)..];

        User? user = await _context.Users
            .AsNoTracking()
            .Include(user => user.Player)
            .FirstOrDefaultAsync(user => user.Email == email);

        if (user is null)
        {
            Logger.LogWarning("Basic authentication failed from IP {RemoteIpAdress}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid email or password.");
        }

        PasswordVerificationResult passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            Logger.LogWarning("Basic authentication failed from IP {RemoteIpAdress}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid email or password.");
        }

        List<Claim> claims = UserClaimsFactory.Create(user, "Basic");

        var identity = new ClaimsIdentity(claims, AuthSchemes.Basic);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(principal, AuthSchemes.Basic);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"GuessingGame.API\"";

        return base.HandleChallengeAsync(properties);
    }
}