using System.ComponentModel.DataAnnotations;

namespace ZipStation.Models.CommandModels;

public class MaxExampleReplyCommandModel
{
    [Required]
    public string ReplyText { get; set; } = string.Empty;

    public string? SourceTicketId { get; set; }

    public string? Notes { get; set; }
}
