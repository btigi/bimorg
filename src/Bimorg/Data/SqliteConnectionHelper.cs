using System.IO;
using Microsoft.Data.Sqlite;

namespace Bimorg.Data;

public static class SqliteConnectionHelper
{
    public static string BuildConnectionString(string? dbPathRaw)
    {
        if (string.IsNullOrWhiteSpace(dbPathRaw))
            throw new InvalidOperationException("DbPath is missing in appsettings.json.");

        var expanded = Environment.ExpandEnvironmentVariables(dbPathRaw.Trim());
        var full = Path.GetFullPath(expanded);

        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return new SqliteConnectionStringBuilder { DataSource = full }.ConnectionString;
    }
}