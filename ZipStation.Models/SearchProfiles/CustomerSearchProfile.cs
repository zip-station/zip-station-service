namespace ZipStation.Models.SearchProfiles;

public class CustomerSearchProfile : BaseSearchProfile
{
    public string? CompanyId { get; set; }
    public string? ProjectId { get; set; }
    public string? Email { get; set; }
    public bool? IsBanned { get; set; }
}
