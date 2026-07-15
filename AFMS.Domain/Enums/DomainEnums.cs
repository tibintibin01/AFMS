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
    Archived = 8
}

public enum TransactionStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Cancelled = 4,
    Rejected = 5,
    Archived = 6
}

public enum ExceptionType
{
    Spoiled = 1,
    Cancelled = 2,
    Lost = 3,
    Missing = 4,
    Damaged = 5,
    NumberingError = 6,
    Other = 7
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
    Exported = 11
}

public enum ApprovalStatus
{
    NotRequired = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4
}
