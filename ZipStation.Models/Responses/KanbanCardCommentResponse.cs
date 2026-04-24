using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class KanbanCardCommentResponse
{
    public string Id { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public KanbanCommentType Type { get; set; }
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public string BodyHtml { get; set; } = string.Empty;
    public List<MessageAttachmentResponse>? Attachments { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
