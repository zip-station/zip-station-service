using ZipStation.Models.Enums;

namespace ZipStation.Models.SearchProfiles;

public class IntakeEmailSearchProfile : BaseSearchProfile
{
    public string? CompanyId { get; set; }
    public string? ProjectId { get; set; }
    public IntakeStatus? Status { get; set; }
    public string? FromEmail { get; set; }
}
