namespace LiveBot;

internal static class CustomLogEvents
{
    public static EventId LiveBot { get; } = new(200, "LiveBot");
    public static EventId CommandExecuted { get; } = new(201, "CMDExecuted");
    public static EventId CommandError { get; } = new(202, "CMDError");
    public static EventId ClientError { get; } = new(203, "ClientError");
    public static EventId SlashExecuted { get; } = new(204, "SlashExecuted");
    public static EventId SlashErrored { get; } = new(205, "SlashErrored");
    public static EventId ContextMenuExecuted { get; } = new(206, "ContextMenuExecuted");
    public static EventId ContextMenuErrored { get; } = new(207, "ContextMenuErrored");
    public static EventId AutoMod { get; } = new(208, "AutoMod");
    public static EventId DeleteLog { get; } = new(209, "DeleteLog");
    public static EventId ModMail { get; } = new(210, "ModMail");
    public static EventId PhotoCleanup { get; } = new(211, "PhotoCleanup");
    public static EventId LiveStream { get; } = new(212, "LiveStream");
    public static EventId AuditLogManager { get; } = new(213, "AuditLogManager");
    public static EventId ServiceError { get; } = new(214, "ServiceError");
    public static EventId TcHub { get; } = new(300, "TCHub");
    public static EventId ModLog { get; } = new(215, "ModLog");
    public static EventId TagCommand { get; } = new(216, "TagCommand");
}