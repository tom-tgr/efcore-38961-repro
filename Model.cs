using Microsoft.EntityFrameworkCore;

namespace EfIssue38961;

// Minimal model matching the shape reported in dotnet/efcore#38961.
public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public List<Comment> Comments { get; set; } = new();
}

public class Comment
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public bool IsVisible { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Text { get; set; } = "";
}

public class EventRow
{
    public int Id { get; set; }
    public CommentDto? LatestComment { get; set; }
}

public class CommentDto
{
    public int EventId { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Text { get; set; } = "";
}

public class ReproContext(string? connectionString = null) : DbContext
{
    // Translation-only tests never open a connection, so any well-formed string works.
    private readonly string _connectionString =
        connectionString ?? "Server=.;Database=EfIssue38961;Trusted_Connection=True;";

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(_connectionString, o => o.CommandTimeout(300));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        // The index an APPLY + TOP(1) plan seeks into once per outer row.
        => modelBuilder.Entity<Comment>()
            .HasIndex(c => new { c.EventId, c.IsVisible, c.CreatedDate })
            .IsDescending(false, false, true);
}
