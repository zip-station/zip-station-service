namespace ZipStation.Models.SearchProfiles;

public class AuditLogSearchProfile : BaseSearchProfile
{
    public string? CompanyId { get; set; }
    public string? ProjectId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public long? FromDate { get; set; }
    public long? ToDate { get; set; }
}
