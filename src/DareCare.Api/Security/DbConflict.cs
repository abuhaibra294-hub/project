using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Security;

public static class DbConflict
{
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } ||
        ex.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 or 1555 };

    // Translate only known transaction/write contention into an HTTP 409.
    // Other storage failures should propagate to error logging.
    public static bool IsRetryableWriteConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 or 1222 or 3960 } ||
                current is SqliteException { SqliteErrorCode: 5 or 6 })
                return true;
        }
        return false;
    }
}
