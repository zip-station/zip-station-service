namespace ZipStation.Models.CommandModels;

public class TicketMessageCommandModel
{
    public string Body { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public bool IsInternalNote { get; set; }
    public bool HasPendingAttachments { get; set; }
}
