using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace EfIssue38961;

// Runs against a real SQL Server; skipped unless EF38961_SQLSERVER holds a connection string, e.g.
//   Server=localhost,1433;Database=EfIssue38961;User Id=sa;Password=...;TrustServerCertificate=True
// Compares the reporter's query with the OUTER APPLY workaround on the SAME EF version.
// Expected: passes on EF 9.x (both are APPLY), fails on EF 10.x (ROW_NUMBER join is much slower).
public class SqlServerPerfTests(ITestOutputHelper output)
{
    private const int Runs = 5;
    private const double AllowedSlowdown = 3.0;
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("EF38961_SQLSERVER");

    [SkippableTheory]
    [InlineData("first-50")]     // consecutive ids: a merge join may stop early, so this can hide the problem
    [InlineData("scattered-50")] // 50 events spread over the table, like a filtered list
    [InlineData("all")]          // every event: one big scan may legitimately compete with 5,000 seeks
    public void Take1_query_is_not_much_slower_than_OUTER_APPLY(string scenario)
    {
        Skip.If(string.IsNullOrEmpty(ConnectionString), "Set EF38961_SQLSERVER to run against SQL Server.");
        SeedData.EnsureSeeded(ConnectionString!);

        var (reported, reportedRows) = Measure(events => Queries.LatestCommentViaTake1(events, includeOuterId: true), scenario);
        var (apply, applyRows) = Measure(Queries.LatestCommentWithOuterReference, scenario);

        output.WriteLine($"scenario={scenario}  reported={reported.TotalMilliseconds:F0}ms  outer-apply={apply.TotalMilliseconds:F0}ms  ratio={reported / apply:F1}x");
        Assert.Equal(applyRows, reportedRows);
        Assert.True(
            reported <= apply * AllowedSlowdown + TimeSpan.FromMilliseconds(20),
            $"Reported query took {reported.TotalMilliseconds:F0}ms vs {apply.TotalMilliseconds:F0}ms for OUTER APPLY.");
    }

    // Median of several runs after one warm-up; returns the rows for a correctness check.
    private (TimeSpan Median, List<string> Rows) Measure(Func<IQueryable<Event>, IQueryable<EventRow>> query, string scenario)
    {
        using var ctx = new ReproContext(ConnectionString);
        IQueryable<Event> events = ctx.Events.AsNoTracking();
        events = scenario switch
        {
            "first-50" => events.OrderBy(e => e.Id).Take(50),
            "scattered-50" => events.Where(e => e.Id % 100 == 0),
            "all" => events,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var q = query(events);
        output.WriteLine(q.ToQueryString() + "\n");

        var rows = Materialize(q);
        var timings = new List<TimeSpan>();
        for (var i = 0; i < Runs; i++)
        {
            var sw = Stopwatch.StartNew();
            Materialize(q);
            timings.Add(sw.Elapsed);
        }

        timings.Sort();
        return (timings[Runs / 2], rows);
    }

    private static List<string> Materialize(IQueryable<EventRow> q)
        => q.AsEnumerable()
            .Select(r => $"{r.Id}|{r.LatestComment?.CreatedDate:O}|{r.LatestComment?.Text}")
            .ToList();
}
