using System.Security.Claims;
using System.Text.Encodings.Web;
using Billing.Infrastructure.Configuration;
using Billing.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Billing.WebApi.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<SecurityOptions> securityOptions) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = securityOptions.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "anonymous-dev")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        if (!Request.Headers.TryGetValue(BillingHeaders.ApiKey, out var provided) ||
            !string.Equals(provided.ToString(), configured, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "service")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
