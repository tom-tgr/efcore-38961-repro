# Repro for dotnet/efcore#38961

EF Core 10 translates a correlated `Take(1)` + `FirstOrDefault()` projection to `LEFT JOIN` + `ROW_NUMBER()`
where EF Core 9 produced `OUTER APPLY` + `TOP(1)`, which is much slower on SQL Server.

```csharp
LatestComment = e.Comments
    .Where(c => c.IsVisible)
    .OrderByDescending(c => c.CreatedDate)
    .Take(1)
    .Select(c => new CommentDto { CreatedDate = c.CreatedDate, Text = c.Text })
    .FirstOrDefault()
```

## Tests

- `TranslationTests`: offline, checks the generated SQL (`ToQueryString()`). The `Take1_*` tests pass on 9.0.21 and fail on 10.0.13.
- `SqlServerPerfTests`: seeds 5,000 events / 1,000,000 comments (index on `EventId, IsVisible, CreatedDate DESC`)
  and compares the query above against an equivalent query that still translates to `OUTER APPLY`.
  Runs only when `EF38961_SQLSERVER` is set; the [workflow](.github/workflows/repro.yml) runs it on SQL Server 2022 for both versions.

```bash
./run.sh                  # 9.0.21 and 10.0.13
./run.sh 10.0.0 10.0.13   # any versions
```

## Results (GitHub Actions, SQL Server 2022, median of 5)

| Outer rows | EF 9.0.21 | EF 10.0.13 | EF 10.0.13 + workaround |
|---|---|---|---|
| 50 events scattered across the table | 2 ms | 1,342 ms | 2 ms |
| All 5,000 events | 56 ms | 1,393 ms | 58 ms |
| First 50 events (by id) | 1 ms | 14 ms | 1 ms |

Workaround: reference an outer column inside the subquery projection (`EventId = e.Id`), which keeps `OUTER APPLY`.
