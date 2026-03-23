namespace ZipStation.Models.SearchProfiles;

public class BaseSearchProfile
{
    public string? Query { get; set; }

    public bool? IsVoid { get; set; }

    public int ResultsPerPage { get; set; } = 25;

    public int Page { get; set; } = 1;

    public string? OrderByFieldName { get; set; }

    public bool OrderByAscending { get; set; } = true;

    public bool OrderByBestMatch { get; set; }
}
