namespace Qx.Model.Wired;

/// <summary>Specifies how a chest's visible open or closed state is controlled.</summary>
public enum WiredChestStateMode
{
    /// <summary>Opens while someone views the contents.</summary>
    OpenWhenViewed = 0,
    /// <summary>Always appears open.</summary>
    AlwaysOpen = 1,
    /// <summary>Always appears closed.</summary>
    AlwaysClosed = 2,
    /// <summary>The visible state is controlled through Wired.</summary>
    WiredControl = 3
}

/// <summary>Specifies which stored furniture appears in an open chest's preview.</summary>
public enum WiredChestPreviewMode
{
    /// <summary>Shows no furniture.</summary>
    None = 0,
    /// <summary>Shows random stored items.</summary>
    Random = 1,
    /// <summary>Shows random items, preferring different furniture types.</summary>
    RandomDistinctTypes = 2,
    /// <summary>Shows the most recently stored items.</summary>
    Newest = 3,
    /// <summary>Shows recent items, preferring different furniture types.</summary>
    NewestDistinctTypes = 4,
    /// <summary>Shows the oldest stored items.</summary>
    Oldest = 5,
    /// <summary>Shows old items, preferring different furniture types.</summary>
    OldestDistinctTypes = 6,
    /// <summary>Shows the next random items to be given through Wired.</summary>
    NextWiredRandom = 7
}

/// <summary>Specifies when chest notifications are delivered.</summary>
public enum WiredChestNotificationMode
{
    /// <summary>Always delivers enabled notifications.</summary>
    Always = 0,
    /// <summary>Delivers enabled notifications only while the owner is outside the room.</summary>
    OnlyWhenAway = 1
}
