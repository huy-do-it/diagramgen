namespace UMLForge.BuildingBlocks.Web;

/// <summary>
/// Query chung cho API danh sách: <c>page</c> (≥1), <c>pageSize</c> (mặc định 20, tối đa 100), <c>search</c>,
/// <c>sort=field:asc|desc</c>. Kế thừa để thêm bộ lọc riêng, nhận bằng <c>[FromQuery]</c>.
/// </summary>
public class PageQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public string? Search { get; set; }

    public string? Sort { get; set; }

    public int Skip => (Page - 1) * PageSize;

    /// <summary>"createdAt:desc" → ("createdAt", true). Sort trống → giá trị mặc định truyền vào.</summary>
    public (string Field, bool Descending) ParseSort(string defaultField, bool defaultDescending = true)
    {
        if (string.IsNullOrWhiteSpace(Sort))
            return (defaultField, defaultDescending);

        var parts = Sort.Split(':', 2, StringSplitOptions.TrimEntries);
        var descending = parts.Length == 2
            ? parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
            : defaultDescending;
        return (parts[0], descending);
    }
}
