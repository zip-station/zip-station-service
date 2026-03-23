using ZipStation.Models.Entities;

namespace ZipStation.Models.Responses;

public class TicketMessageResponse
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public bool IsInternalNote { get; set; }
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public MessageSource Source { get; set; }
    public MessageSendStatus SendStatus { get; set; }
    public string? SendError { get; set; }
    public long SentOnDateTime { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
