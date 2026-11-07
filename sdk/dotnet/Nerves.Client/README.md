# Nerves.Client (.NET)

Chainable, dependency-free .NET 10 client for the Nerves management API. It uses
only `System.Net.Http` and `System.Text.Json`.

```csharp
using Nerves.Client;

using var nerves = new NervesClient(new NervesClientOptions
{
    BaseUrl = new Uri("https://accounts.example.com"),
    Token = Environment.GetEnvironmentVariable("NERVES_TOKEN"),
});

var account = await nerves.Accounts
    .Create()
    .ForEntity(legalEntityId)
    .InRealm("default")
    .WithUsername("alice")
    .WithEmail("alice@example.com")
    .WithRoles("member")
    .SendAsync(cancellationToken);

var page = await nerves.Accounts
    .List()
    .ForEntity(legalEntityId)
    .WithStatus(AccountStatus.Active)
    .Filter("username", "alice")
    .Sort("-createdAt")
    .Limit(50)
    .SendAsync(cancellationToken);

await foreach (var item in nerves.Accounts.List().ForEntity(legalEntityId).StreamAsync(cancellationToken))
{
    Console.WriteLine(item.Username);
}
```

Inject an `HttpClient` to share a handler, tracing, proxy, or test double:

```csharp
var nerves = new NervesClient(new NervesClientOptions
{
    BaseUrl = new Uri("https://accounts.example.com"),
    HttpClient = httpClient, // not disposed by NervesClient
    Token = token,
    MaxRetries = 2,
    RetryBaseDelay = TimeSpan.FromMilliseconds(200),
});
```

Builders are immutable records: `builder.WithEmail(x)` returns a copy and the
original can be reused. Local validation fails fast (for example a non-service
account without `ForEntity`), and every non-success response throws
`NervesApiException` with `Status`, `Type`, `TraceId`, and the parsed
`NervesProblemDetails`.

## Tests

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test sdk/dotnet/Nerves.Client.Tests/Nerves.Client.Tests.csproj
```

Tests use xunit.v3 + Shouldly and a recording `HttpMessageHandler`.