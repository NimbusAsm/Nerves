using Microsoft.Extensions.Options;

namespace Nerves.Identity.Api;

/// <summary>Requires a bearer token matching the configured admin token.</summary>
public sealed class AdminTokenFilter(IOptions<AdminTokenOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.AdminToken;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return ValueTask.FromResult<object?>(
                Results.Problem("Management API token is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable));
        }

        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        var presented = header["Bearer ".Length..].Trim();
        var provided = System.Text.Encoding.UTF8.GetBytes(presented);
        var required = System.Text.Encoding.UTF8.GetBytes(expected);
        var matches = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(provided, required);

        return matches ? next(context) : ValueTask.FromResult<object?>(Results.Unauthorized());
    }
}
