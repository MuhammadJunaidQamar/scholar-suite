using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Scholar.Application.Common;

/// <summary>Common list parameters. Bound from the query string with <c>[AsParameters]</c>.</summary>
public record PageQuery
{
    private const int MaxPageSize = 200;

    // Nullable so query-string binding treats them as optional (minimal APIs require non-nullable value types).
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Sort { get; init; }
    public string? Dir { get; init; }
    public string? Search { get; init; }

    public int SafePage => Page is null or < 1 ? 1 : Page.Value;
    public int SafePageSize => PageSize switch { null or < 1 => 25, > MaxPageSize => MaxPageSize, _ => PageSize.Value };
    public bool Descending => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Normalised search term (trimmed, null when empty).</summary>
    public string? Term => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Maps public sort keys to expressions so clients can never sort by arbitrary columns.</summary>
public sealed class SortMap<T> : Dictionary<string, Expression<Func<T, object?>>>
{
    public SortMap() : base(StringComparer.OrdinalIgnoreCase) { }
}

public static class QueryableExtensions
{
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, PageQuery page, SortMap<T> map, string defaultKey,
        bool defaultDescending = false)
    {
        var key = page.Sort is not null && map.ContainsKey(page.Sort) ? page.Sort : defaultKey;
        var descending = page.Sort is not null && map.ContainsKey(page.Sort) ? page.Descending : defaultDescending;
        var expression = map[key];
        var ordered = descending ? query.OrderByDescending(expression) : query.OrderBy(expression);

        // Tie-break on the (time-ordered) key so pages never repeat or skip rows when many share a sort value,
        // e.g. 30 students in the same class.
        if (typeof(T).GetProperty("Id") is not { } id)
        {
            return ordered;
        }

        var row = Expression.Parameter(typeof(T), "row");
        var byId = Expression.Lambda(Expression.Property(row, id), row);
        return ordered.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy),
            [typeof(T), id.PropertyType], ordered.Expression, Expression.Quote(byId)));
    }

    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(this IQueryable<TIn> query, PageQuery page,
        Expression<Func<TIn, TOut>> projection, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page.SafePage - 1) * page.SafePageSize)
            .Take(page.SafePageSize)
            .Select(projection)
            .ToListAsync(ct);
        return new PagedResult<TOut>(items, page.SafePage, page.SafePageSize, total);
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate) =>
        condition ? query.Where(predicate) : query;
}
