namespace ZipStation.Models.Responses;

public class TicketDetailResponse
{
    public TicketResponse Ticket { get; set; } = new();
    public List<TicketMessageResponse> Messages { get; set; } = new();
}
