namespace ArielVertex.Application.Common;

/// <summary>Standard envelope for all list endpoints (spec section 12: pagination everywhere).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public static PagedResult<T> Empty(int page, int pageSize) => new(Array.Empty<T>(), 0, page, pageSize);
}

public record PageQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null)
{
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafeSize => PageSize is < 1 or > 100 ? 20 : PageSize;
    public int Skip => (SafePage - 1) * SafeSize;
}
