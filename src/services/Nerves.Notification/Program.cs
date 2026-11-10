using Microsoft.EntityFrameworkCore;
using Nerves.Core;
using Nerves.Notification.Api;
using Nerves.Notification.Delivery;
using Nerves.Notification.Options;
using Nerves.Notification.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-notification");
builder.Services.AddNervesProxyHeaders(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<DeliveryOptions>(builder.Configuration.GetSection(DeliveryOptions.SectionName));

var smtpOptions = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
var smtpValidation = smtpOptions.Validate();
if (smtpValidation.IsFailure)
{
    throw new InvalidOperationException(smtpValidation.Reason.Message);
}

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<DeliveryAttemptRunner>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddHostedService<NotificationWorker>();

// Development runs without a mail server: Smtp:Disabled=true selects the sender
// that logs and reports success.
if (smtpOptions.Disabled)
{
    builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<NotificationDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();

if (await app.MigrateNervesDatabaseAsync<NotificationDbContext>())
{
    return;
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-notification");
app.MapNotificationEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
