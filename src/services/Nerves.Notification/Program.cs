using Microsoft.EntityFrameworkCore;
using Nerves.Core;
using Nerves.Notification.Api;
using Nerves.Notification.Consumption;
using Nerves.Notification.Delivery;
using Nerves.Notification.Options;
using Nerves.Notification.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-notification");
builder.Services.AddNervesProxyHeaders(builder.Configuration);
builder.Services.AddNervesRateLimiting(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<DeliveryOptions>(builder.Configuration.GetSection(DeliveryOptions.SectionName));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.Configure<EventConsumerOptions>(builder.Configuration.GetSection(EventConsumerOptions.SectionName));

var smtpOptions = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
var smtpValidation = smtpOptions.Validate();
if (smtpValidation.IsFailure)
{
    throw new InvalidOperationException(smtpValidation.Reason.Message);
}

var smsOptions = builder.Configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>() ?? new SmsOptions();
var smsValidation = smsOptions.Validate();
if (smsValidation.IsFailure)
{
    throw new InvalidOperationException(smsValidation.Reason.Message);
}

var eventsOptions = builder.Configuration.GetSection(EventConsumerOptions.SectionName).Get<EventConsumerOptions>()
                    ?? new EventConsumerOptions();
var eventsValidation = eventsOptions.Validate();
if (eventsValidation.IsFailure)
{
    throw new InvalidOperationException(eventsValidation.Reason.Message);
}

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<MessageSenderRouter>();
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

builder.Services.AddSingleton<IMessageSender, EmailMessageSender>();

// SMS is opt-in. Disabled (the default) wires the no-op sender, so the API can
// still be exercised in development without a gateway.
if (smsOptions.Disabled)
{
    builder.Services.AddSingleton<ISmsSender, NoOpSmsSender>();
}
else
{
    builder.Services.AddHttpClient(SmsSender.HttpClientName);
    builder.Services.AddSingleton<ISmsSender, SmsSender>();
}

builder.Services.AddSingleton<IMessageSender, SmsMessageSender>();

// The identity event consumer is opt-in and needs a token that can read the
// event stream; it is not registered at all while disabled.
if (eventsOptions.Enabled)
{
    builder.Services.AddHttpClient(AccessTokenProvider.HttpClientName);
    builder.Services.AddSingleton<AccessTokenProvider>();
    builder.Services.AddSingleton<IIdentityEventClient, IdentityEventClient>();
    builder.Services.AddScoped<INotificationStore, EfNotificationStore>();
    builder.Services.AddScoped<EventConsumer>();
    builder.Services.AddHostedService<EventConsumerWorker>();
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
app.UseNervesRateLimiting();

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
