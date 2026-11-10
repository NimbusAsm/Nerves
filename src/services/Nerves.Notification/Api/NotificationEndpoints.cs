using Microsoft.EntityFrameworkCore;
using Nerves.Core;
using Nerves.Notification.Domain;
using Nerves.Notification.Persistence;
using Nerves.Notification.Templates;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Notification.Api;

/// <summary>Management API for queueing notifications and reading the delivery log.</summary>
public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        // Kept for the existing console and smoke script; equivalent to channel=email.
        group.MapPost("/email", SendEmailAsync);

        // Channel-explicit entry point: email or sms.
        group.MapPost("/send", SendAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);

        return group;
    }

    private static Task<IResult> SendEmailAsync(
        SendEmailRequest request,
        NotificationDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Subject) || !string.IsNullOrWhiteSpace(request.Body))
        {
            return Task.FromResult(Results.Problem(
                "custom subject/body are not supported; send a template code instead",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return QueueAsync(
            NotificationChannel.Email,
            request.To,
            request.Template,
            request.Values,
            db,
            clock,
            cancellationToken);
    }

    private static Task<IResult> SendAsync(
        SendMessageRequest request,
        NotificationDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!NotificationChannelWire.TryParse(request.Channel, out var channel))
        {
            return Task.FromResult(Results.Problem(
                "channel must be one of email or sms",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return QueueAsync(channel, request.To, request.Template, request.Values, db, clock, cancellationToken);
    }

    private static async Task<IResult> QueueAsync(
        NotificationChannel channel,
        string? recipient,
        string? templateCode,
        IReadOnlyDictionary<string, string>? values,
        NotificationDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var template = EmailTemplateCatalog.Render(templateCode ?? string.Empty, values);
        if (template.IsFailure)
        {
            return Results.Problem(template.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var now = clock.UtcNow;
        NotificationMessage message;

        if (channel == NotificationChannel.Sms)
        {
            var phone = PhoneNumber.Create(recipient);
            if (phone.IsFailure)
            {
                return Results.Problem(phone.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            message = NotificationMessage.Create(
                channel,
                phone.Value.Value,
                template.Value.Template,
                template.Value.Subject,
                template.Value.Body,
                now);
        }
        else
        {
            var address = EmailAddress.Create(recipient);
            if (address.IsFailure)
            {
                return Results.Problem(address.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            message = NotificationMessage.Create(
                address.Value,
                template.Value.Template,
                template.Value.Subject,
                template.Value.Body,
                now);
        }

        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/notifications/{message.Id}", ToResponse(message));
    }

    private static async Task<IResult> ListAsync(
        NotificationDbContext db,
        string? status = null,
        string? channel = null,
        string? to = null,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.Messages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
            {
                return Results.Problem(
                    "status must be one of pending, sent or failed",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(x => x.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            if (!NotificationChannelWire.TryParse(channel, out var parsedChannel))
            {
                return Results.Problem(
                    "channel must be one of email or sms",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(x => x.Channel == parsedChannel);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            var normalized = to.Trim().ToLowerInvariant();
            query = query.Where(x => x.To == normalized);
        }

        // Cursor paging by id: UUIDv7 keeps insertion order, so the newest page is
        // stable even while new messages arrive at the head.
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            query = query.Where(x => x.Id.CompareTo(cursorId) < 0);
        }

        var items = await query
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore)
        {
            items = items.Take(limit).ToList();
        }

        var page = new PageResponse<NotificationResponse>(
            items.Select(ToResponse).ToList(),
            hasMore ? items[^1].Id.ToString() : null);

        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        NotificationDbContext db,
        CancellationToken cancellationToken)
    {
        var message = await db.Messages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return message is null ? Results.NotFound() : Results.Ok(ToResponse(message));
    }

    private static bool TryParseStatus(string value, out NotificationStatus status)
    {
        status = value.Trim().ToLowerInvariant() switch
        {
            "pending" => NotificationStatus.Pending,
            "sent" => NotificationStatus.Sent,
            "failed" => NotificationStatus.Failed,
            _ => (NotificationStatus)(-1),
        };

        return Enum.IsDefined(status);
    }

    private static NotificationResponse ToResponse(NotificationMessage message) => new(
        message.Id,
        message.To,
        message.Channel.ToWire(),
        message.Template,
        message.Subject,
        message.Status.ToWire(),
        message.Attempts,
        message.Error,
        message.SourceEventId,
        message.CreatedAt,
        message.SentAt,
        message.NextAttemptAt);
}
