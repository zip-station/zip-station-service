namespace ZipStation.Models.Responses;

public class KanbanCardDetailResponse
{
    public KanbanCardResponse Card { get; set; } = new();
    public List<KanbanCardCommentResponse> Comments { get; set; } = new();
    public List<TicketResponse> LinkedTickets { get; set; } = new();
}
