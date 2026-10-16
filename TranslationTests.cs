using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace EfIssue38961;

// Offline: inspects the SQL EF Core generates, no database needed.
// Expected: the Take1 tests PASS on EF 9.x and FAIL on EF 10.x (the regression).
public class TranslationTests(ITestOutputHelper output)
{
    private static readonly string EfVersion =
        typeof(DbContext).Assembly.GetName().Version!.ToString();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Take1_then_FirstOrDefault_translates_to_OUTER_APPLY(bool includeOuterId)
    {
        var sql = Translate(ctx => Queries.LatestCommentViaTake1(ctx.Events, includeOuterId));

        Assert.Contains("OUTER APPLY", sql);
        Assert.DoesNotContain("ROW_NUMBER()", sql);
    }

    [Fact]
    public void FirstOrDefault_without_Take1_uses_ROW_NUMBER_on_every_version()
    {
        // Pre-existing behaviour (EF 5+): shows the slow shape itself is not new in EF10.
        var sql = Translate(ctx => Queries.LatestCommentWithoutTake1(ctx.Events));

        Assert.Contains("ROW_NUMBER()", sql);
    }

    [Fact]
    public void Outer_reference_inside_subquery_keeps_OUTER_APPLY()
    {
        var sql = Translate(ctx => Queries.LatestCommentWithOuterReference(ctx.Events));

        Assert.Contains("OUTER APPLY", sql);
        Assert.DoesNotContain("ROW_NUMBER()", sql);
    }

    private string Translate<T>(Func<ReproContext, IQueryable<T>> query)
    {
        using var ctx = new ReproContext();
        var sql = query(ctx).ToQueryString();
        output.WriteLine($"EF Core {EfVersion}\n{sql}");
        return sql;
    }
}
