using MesseLeads.Mobile.Models;
using SQLite;

namespace MesseLeads.Mobile.Data;

public sealed class LocalDatabaseService
{
    private SQLiteAsyncConnection? _database;

    public async Task<SQLiteAsyncConnection> GetDatabaseAsync()
    {
        if (_database is not null)
        {
            return _database;
        }

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "messeleads_local.db3");

        _database = new SQLiteAsyncConnection(
            dbPath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache);

        await _database.CreateTableAsync<LocalLead>();
        await _database.CreateTableAsync<LocalLeadImage>();
        await _database.CreateTableAsync<LocalLookupItem>();
        await _database.CreateTableAsync<LocalLookupGroup>();
        await _database.CreateTableAsync<LocalSyncHistory>();
        await EnsureColumnAsync(_database, "LocalLead", "TradeFairKey", "TEXT");
        await EnsureColumnAsync(_database, "LocalLookupItem", "TradeFairKey", "TEXT");
        await EnsureColumnAsync(_database, "LocalLookupItem", "FieldType", "INTEGER");
        await EnsureColumnAsync(_database, "LocalLookupItem", "OptionA", "TEXT");
        await EnsureColumnAsync(_database, "LocalLookupItem", "OptionB", "TEXT");
        await EnsureColumnAsync(_database, "LocalLookupGroup", "TradeFairKey", "TEXT");

        return _database;
    }

    private static async Task EnsureColumnAsync(
        SQLiteAsyncConnection database,
        string tableName,
        string columnName,
        string columnType)
    {
        var columns = await database.QueryAsync<TableColumnInfo>($"PRAGMA table_info({tableName})");

        if (columns.Any(x => string.Equals(x.Name, columnName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await database.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}");
    }

    private sealed class TableColumnInfo
    {
        [Column("name")]
        public string Name { get; set; } = "";
    }
}
