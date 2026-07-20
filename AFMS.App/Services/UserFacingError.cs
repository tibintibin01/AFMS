using AFMS.Application.Validation;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AFMS.App.Services;

public static class UserFacingError
{
    public static string From(Exception exception)
    {
        Log.Error(exception, "AFMS operation failed");
        return exception switch
        {
            ApplicationValidationException validation => string.Join(Environment.NewLine, validation.Errors),
            UnauthorizedAccessException => exception.Message,
            InvalidOperationException => exception.Message,
            KeyNotFoundException => exception.Message,
            IOException => "AFMS could not access the required file or folder. Check the configured location and permissions.",
            DbUpdateConcurrencyException => "This record changed in another operation. Refresh the screen and try again.",
            DbUpdateException or SqliteException => "The database could not complete the operation safely. No partial change was accepted. An administrator can review the technical log.",
            _ => "The operation could not be completed. Technical details were recorded in the AFMS log."
        };
    }
}
