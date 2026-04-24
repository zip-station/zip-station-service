namespace ZipStation.Models.CommandModels;

public class KanbanCardCommentCommandModel : BaseCommandModel
{
    public string BodyHtml { get; set; } = string.Empty;
}
