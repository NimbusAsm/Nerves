using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Audit.V1;
using Nerves.Contracts.Authorization.V1;
using Nerves.Contracts.Identity.V1;
using Nerves.Contracts.Session.V1;
using Nerves.Idp.Api;
using Nerves.Idp.Federation;
using Nerves.Idp.Integration;
using Nerves.Idp.Logout;
using Nerves.Idp.Options;
using Nerves.Idp.Persistence;
using Nerves.Idp.Security;
using Nerves.ServiceDefaults;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var signingKey = new Nerves.Idp.Security.SigningKeyProvider(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(signingKey);

builder.AddNervesDefaults("nerves-idp");

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddNervesDataProtection(builder.Configuration, builder.Environment);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "nerves.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var adminOrigins = builder.Configuration.GetSection("Cors:AdminOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(adminOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter =>
{
    limiter.PermitLimit = 10;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<IdpDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddScoped<LoginTickets>();
builder.Services.AddScoped<FederationStateStore>();
builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection(LdapOptions.SectionName));
builder.Services.AddSingleton<LdapDirectory>();
builder.Services.Configure<FederationOptions>(builder.Configuration.GetSection(FederationOptions.SectionName));
builder.Services.AddSingleton<LogoutTokenIssuer>();
builder.Services.AddSingleton<BackchannelLogoutDispatcher>();
builder.Services.AddScoped<IntegrationSettingsStore>();
builder.Services.AddHttpClient("backchannel-logout", client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.Configure<BackchannelLogoutOptions>(builder.Configuration.GetSection(BackchannelLogoutOptions.SectionName));

builder.Services.AddOpenIddict()
    .AddCore(options => options
        .UseEntityFrameworkCore()
        .UseDbContext<IdpDbContext>())
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    })
    .AddServer(options =>
    {
        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            options.SetIssuer(new Uri(issuer));
        }

        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetEndSessionEndpointUris("/connect/logout");

        options
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles,
            "nerves.api");

        // In-memory keys are acceptable for the skeleton; production wiring to
        // certificates or a key store lands with the deployment milestone.
        options.AddEphemeralEncryptionKey();
        options.AddSigningKey(signingKey.Key);

        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
            .DisableTransportSecurityRequirement()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
    });

builder.Services.Configure<OpenIddictSeedOptions>(
    builder.Configuration.GetSection(OpenIddictSeedOptions.SectionName));
builder.Services.Configure<IdentityClientOptions>(
    builder.Configuration.GetSection(IdentityClientOptions.SectionName));

var grpcAddress = builder.Configuration
    .GetSection(IdentityClientOptions.SectionName)["GrpcAddress"] ?? "http://127.0.0.1:5102";

var sessionAddress = builder.Configuration["Session:GrpcAddress"] ?? "http://127.0.0.1:5203";
var authorizationAddress = builder.Configuration["Authorization:GrpcAddress"] ?? "http://127.0.0.1:5204";

builder.Services.AddSingleton(_ => new IdentityService.IdentityServiceClient(GrpcChannel.ForAddress(grpcAddress)));
builder.Services.AddSingleton(_ => new SessionService.SessionServiceClient(GrpcChannel.ForAddress(sessionAddress)));
builder.Services.AddSingleton(_ => new AuthorizationService.AuthorizationServiceClient(GrpcChannel.ForAddress(authorizationAddress)));

var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";

builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<AuditClient>();

builder.Services.AddSingleton<IdentityDirectory>();
builder.Services.AddSingleton<SessionDirectory>();
builder.Services.AddSingleton<AuthorizationDirectory>();
builder.Services.AddHostedService<OpenIddictClientSeeder>();

var app = builder.Build();

await app.MigrateNervesDatabaseAsync<IdpDbContext>();

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var store = startupScope.ServiceProvider.GetRequiredService<IntegrationSettingsStore>();
    var ldapOptions = startupScope.ServiceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<LdapOptions>>().Value;
    await store.ApplyAsync(ldapOptions, CancellationToken.None);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-idp");
app.MapControllers();
app.MapAuthEndpoints();
app.MapFederationEndpoints();
app.MapIntegrationStatusEndpoints();
app.MapIntegrationSettingsEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
