namespace AFMS.Domain.Enums;

public enum AccountableFormStatus
{
    Available = 1,
    Issued = 2,
    PartiallyUsed = 3,
    FullyUsed = 4,
    Returned = 5,
    Transferred = 6,
    Spoiled = 7,
    Cancelled = 8,
    Lost = 9,
    Missing = 10,
    Archived = 11
}

public enum StockMovementType
{
    Received = 1,
    Issued = 2,
    Used = 3,
    Transferred = 4,
    Returned = 5,
    Adjusted = 6,
    Cancelled = 7,
    Archived = 8,
    Reversed = 9
}

public enum TransactionStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Cancelled = 4,
    Rejected = 5,
    Archived = 6,
    Submitted = 7,
    Finalized = 8,
    Reversed = 9,
    Approved = 10
}

public enum ExceptionType
{
    Spoiled = 1,
    Cancelled = 2,
    Lost = 3,
    Missing = 4,
    Damaged = 5,
    NumberingError = 6,
    Other = 7,
    Invalidated = 8
}

public enum ReturnDisposition
{
    ReturnedToAvailableStock = 1,
    ReturnedButDamaged = 2,
    ForCancellation = 3,
    Archived = 4,
    SubjectToInvestigation = 5
}

public enum ExceptionResolutionStatus
{
    Open = 1,
    UnderInvestigation = 2,
    Resolved = 3,
    Closed = 4
}

public enum AttachmentTransactionType
{
    Transfer = 1,
    Return = 2,
    FormException = 3
}

public enum UserActivityType
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    Logout = 3,
    AccountCreated = 4,
    AccountUpdated = 5,
    PasswordChanged = 6,
    RecordCreated = 7,
    RecordUpdated = 8,
    RecordDeleted = 9,
    RecordViewed = 10,
    Exported = 11,
    Activated = 12,
    Deactivated = 13,
    PasswordReset = 14,
    RoleAssigned = 15,
    SystemSettingChanged = 16,
    Printed = 17,
    ReversalAttempt = 18,
    Submitted = 19,
    Approved = 20,
    Rejected = 21,
    Finalized = 22,
    Reversed = 23,
    BackupCreated = 24,
    BackupValidated = 25,
    RestoreRequested = 26,
    Restored = 27,
    PermissionChanged = 28,
    FiscalYearCloseRequested = 29,
    FiscalYearClosed = 30,
    FiscalYearReopenRequested = 31,
    FiscalYearReopened = 32,
    SessionExpired = 33
}

public enum ApprovalStatus
{
    NotRequired = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4
}

public enum SystemPermission
{
    ViewDashboard = 1,
    ManageFormTypes = 2,
    ManageCollectors = 3,
    ManageBarangays = 4,
    ReceiveStock = 5,
    IssueStock = 6,
    RecordUsage = 7,
    TransferStock = 8,
    ReturnStock = 9,
    ManageExceptions = 10,
    ApproveTransactions = 11,
    ViewReports = 12,
    ExportReports = 13,
    ManageUsers = 14,
    ViewAuditLogs = 15,
    BackupAndRestore = 16,
    ManageFiscalYears = 17,
    ManageSystemSettings = 18,
    ViewInventory = 19,
    ViewStockMovements = 20,
    ViewIssuances = 21,
    ViewCollectorAccountability = 22,
    ReverseIssuance = 23,
    ReverseOperationalTransactions = 24,
    CloseFiscalYear = 25,
    RequestFiscalYearReopen = 26,
    ApproveFiscalYearReopen = 27
}

public enum BackupKind
{
    Manual = 1,
    Scheduled = 2,
    ApplicationClosing = 3,
    PreRestore = 4,
    PreMigration = 5
}

public enum FiscalYearWorkflowStatus
{
    Open = 1,
    ClosePending = 2,
    Closed = 3,
    ReopenPending = 4
}
