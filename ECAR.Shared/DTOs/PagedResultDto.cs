namespace ECAR.Shared.DTOs;

public class PagedResultDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class PagedResultDto<T> : PagedResultDto
{
    public List<T> Data { get; set; } = new();
}