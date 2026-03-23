namespace ZipStation.Models.Responses;

public class PaginatedResponse<T>
{
    public long TotalResultCount { get; set; }

    public List<T> Results { get; set; } = new();
}
