namespace AFMS.Core.Domain;

/// <summary>Whether a recorded line adds to or takes away from stock on hand.</summary>
public enum MovementDirection
{
    In = 1,
    Out = 2
}

/// <summary>Where incoming stock came from. <see cref="Returned"/> closes an earlier release.</summary>
public enum StockInSource
{
    /// <summary>Bought from or delivered by a supplier.</summary>
    Purchase = 1,

    /// <summary>Supplied by another office, the province, or a national agency.</summary>
    Transferred = 2,

    /// <summary>Handed back by the person it was released to.</summary>
    Returned = 3,

    /// <summary>Stock already on hand when record keeping started in this app.</summary>
    BeginningBalance = 4,

    /// <summary>Correction after a physical count.</summary>
    Adjustment = 5
}

/// <summary>
/// Lifecycle of a recorded document. Records are never deleted. A mistake is voided
/// with a reason so both the original entry and the correction stay on file.
/// </summary>
public enum RecordStatus
{
    Recorded = 1,
    Voided = 2
}

/// <summary>Actions written to the audit trail.</summary>
public enum ActivityType
{
    SignedIn = 1,
    SignInFailed = 2,
    SignedOut = 3,
    SessionExpired = 4,
    PasswordChanged = 5,
    AccountCreated = 6,
    ItemCreated = 10,
    ItemUpdated = 11,
    ItemStatusChanged = 12,
    RecipientCreated = 20,
    RecipientUpdated = 21,
    RecipientStatusChanged = 22,
    StockInRecorded = 30,
    StockInUpdated = 31,
    StockInVoided = 32,
    ReleaseRecorded = 40,
    ReleaseUpdated = 41,
    ReleaseVoided = 42,
    SettingsUpdated = 50,
    BackupCreated = 60,
    RestoreRequested = 61,
    RestoreCompleted = 62,
    ReportExported = 70
}

/// <summary>Why a backup was taken.</summary>
public enum BackupKind
{
    Manual = 1,
    Daily = 2,
    OnClosing = 3,
    BeforeRestore = 4,
    BeforeUpgrade = 5
}
