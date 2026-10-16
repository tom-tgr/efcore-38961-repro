using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfIssue38961;

// Creates (once) a database big enough for the APPLY vs ROW_NUMBER plans to diverge.
public static class SeedData
{
    public const int EventCount = 5_000;
    public const int CommentsPerEvent = 200; // 1,000,000 comments

    public static void EnsureSeeded(string connectionString)
    {
        using var ctx = new ReproContext(connectionString);
        if (ctx.Database.CanConnect()
            && ctx.Comments.Count() == EventCount * CommentsPerEvent)
        {
            return;
        }

        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        using var connection = new SqlConnection(ctx.Database.GetConnectionString());
        connection.Open();
        BulkInsert(connection, "Events", BuildEvents());
        BulkInsert(connection, "Comments", BuildComments());
        new SqlCommand("UPDATE STATISTICS Comments WITH FULLSCAN", connection).ExecuteNonQuery();
    }

    private static DataTable BuildEvents()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Title", typeof(string));
        for (var i = 1; i <= EventCount; i++)
        {
            table.Rows.Add(i, $"Event {i}");
        }

        return table;
    }

    private static DataTable BuildComments()
    {
        var random = new Random(42);
        var start = new DateTime(2025, 1, 1);
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("EventId", typeof(int));
        table.Columns.Add("IsVisible", typeof(bool));
        table.Columns.Add("CreatedDate", typeof(DateTime));
        table.Columns.Add("Text", typeof(string));
        var id = 1;
        for (var e = 1; e <= EventCount; e++)
        {
            for (var c = 0; c < CommentsPerEvent; c++)
            {
                // Unique timestamps per event, so "latest" is deterministic across query plans.
                table.Rows.Add(id++, e, random.NextDouble() < 0.9, start.AddMinutes(c), $"Comment {c} on event {e}");
            }
        }

        return table;
    }

    private static void BulkInsert(SqlConnection connection, string tableName, DataTable rows)
    {
        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, null)
        {
            DestinationTableName = tableName,
            BatchSize = 50_000,
            BulkCopyTimeout = 0
        };
        foreach (DataColumn column in rows.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        bulk.WriteToServer(rows);
    }
}
