namespace Nerves.Notification.Templates;

/// <summary>Subject and plain-text body produced by a template render.</summary>
public sealed record RenderedEmail(string Template, string Subject, string Body);
