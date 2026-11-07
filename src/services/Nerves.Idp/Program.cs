using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Identity.V1;
using Nerves.Idp.Integration;
using Nerves.Idp.Options;
using Nerves.Idp.Persistence;
using Nerves.ServiceDefaults;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-idp");

builder.Services.AddOpenApi();
builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "nerves.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<IdpDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddOpenIddict()
    .AddCore(options => options
        .UseEntityFrameworkCore()
        .UseDbContext<IdpDbContext>())
    .AddServer(options =>
    {
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
        options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
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

builder.Services.AddSingleton(_ => GrpcChannel.ForAddress(grpcAddress));
builder.Services.AddSingleton(sp =>
    new IdentityService.IdentityServiceClient(sp.GetRequiredService<GrpcChannel>()));
builder.Services.AddSingleton<IdentityDirectory>();
builder.Services.AddHostedService<OpenIddictClientSeeder>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-idp");
app.MapRazorPages();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
