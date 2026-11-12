namespace UMLForge.BuildingBlocks.Web;

/// <summary>Kết quả danh sách có phân trang (TASKS §1.3).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
        => new(items, page, pageSize, totalItems,
            pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
}
