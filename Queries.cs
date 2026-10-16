namespace EfIssue38961;

// The LINQ shapes under test. Each takes the outer events query so tests can page it.
public static class Queries
{
    // Exactly the reporter's shape: Take(1) followed by FirstOrDefault().
    public static IQueryable<EventRow> LatestCommentViaTake1(IQueryable<Event> events, bool includeOuterId)
        => includeOuterId
            ? events.Select(e => new EventRow
            {
                Id = e.Id,
                LatestComment = e.Comments
                    .Where(c => c.IsVisible)
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(1)
                    .Select(c => new CommentDto { CreatedDate = c.CreatedDate, Text = c.Text })
                    .FirstOrDefault()
            })
            : events.Select(e => new EventRow
            {
                LatestComment = e.Comments
                    .Where(c => c.IsVisible)
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(1)
                    .Select(c => new CommentDto { CreatedDate = c.CreatedDate, Text = c.Text })
                    .FirstOrDefault()
            });

    // Control: no explicit Take(1). EF9 already translated this to ROW_NUMBER().
    public static IQueryable<EventRow> LatestCommentWithoutTake1(IQueryable<Event> events)
        => events.Select(e => new EventRow
        {
            Id = e.Id,
            LatestComment = e.Comments
                .Where(c => c.IsVisible)
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new CommentDto { CreatedDate = c.CreatedDate, Text = c.Text })
                .FirstOrDefault()
        });

    // Workaround: an outer column referenced inside the subquery blocks the APPLY -> JOIN rewrite.
    public static IQueryable<EventRow> LatestCommentWithOuterReference(IQueryable<Event> events)
        => events.Select(e => new EventRow
        {
            Id = e.Id,
            LatestComment = e.Comments
                .Where(c => c.IsVisible)
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new CommentDto { EventId = e.Id, CreatedDate = c.CreatedDate, Text = c.Text })
                .FirstOrDefault()
        });
}
