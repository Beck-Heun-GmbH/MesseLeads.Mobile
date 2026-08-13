namespace MesseLeads.Mobile.Services;

public enum LeadSaveNotificationKind
{
    Syncing,
    Synced,
    PartiallySynced,
    LocalOnly
}

public sealed record LeadSaveNotification(LeadSaveNotificationKind Kind);

public static class LeadSaveNotificationState
{
    private static LeadSaveNotification? _pendingNotification;
    public static event Action<LeadSaveNotification>? Published;

    public static void Set(LeadSaveNotificationKind kind)
    {
        _pendingNotification = new LeadSaveNotification(kind);
    }

    public static void Publish(LeadSaveNotificationKind kind)
    {
        Published?.Invoke(new LeadSaveNotification(kind));
    }

    public static LeadSaveNotification? Consume()
    {
        var notification = _pendingNotification;
        _pendingNotification = null;
        return notification;
    }
}
