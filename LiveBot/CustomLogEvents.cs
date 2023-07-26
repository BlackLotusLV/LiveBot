namespace LiveBot
{
    internal static class CustomLogEvents
    {
        public static EventId LiveBot { get; } = new EventId(200, "LiveBot");
        public static EventId CommandExecuted { get; } = new EventId(201, "CMDExecuted");
        public static EventId CommandError { get; } = new EventId(202, "CMDError");
        public static EventId ClientError { get; } = new EventId(203, "ClientError");
        public static EventId SlashExecuted { get; } = new EventId(204, "SlashExecuted");
        public static EventId SlashErrored { get; } = new EventId(205, "SlashErrored");
        public static EventId ContextMenuExecuted { get; } = new EventId(206, "ContextMenuExecuted");
        public static EventId ContextMenuErrored { get; } = new EventId(207, "ContextMenuErrored");
        public static EventId AutoMod { get; } = new EventId(208, "AutoMod");
        public static EventId DeleteLog { get; } = new EventId(209, "DeleteLog");
        public static EventId ModMail { get; } = new EventId(210, "ModMail");
        public static EventId PhotoCleanup { get; } = new EventId(211, "PhotoCleanup");
        public static EventId LiveStream { get; } = new EventId(212, "LiveStream");
        public static EventId AuditLogManager { get; } = new EventId(213, "AuditLogManager");
        public static EventId TcHub { get; } = new EventId(300, "TCHub");
    }
}