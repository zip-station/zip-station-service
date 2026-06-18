namespace ZipStation.Models.Responses;

public class KanbanBoardResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public List<KanbanColumnResponse> Columns { get; set; } = new();
    public string ResolvedColumnId { get; set; } = string.Empty;
    public string IntakeColumnId { get; set; } = string.Empty;
    public List<KanbanCardTypeResponse> CustomCardTypes { get; set; } = new();
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class KanbanColumnResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int Position { get; set; }
}

public class KanbanCardTypeResponse
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Color { get; set; }
}
