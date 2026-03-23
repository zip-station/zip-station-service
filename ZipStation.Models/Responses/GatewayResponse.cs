namespace ZipStation.Models.Responses;

public class GatewayResponse
{
    public GatewayResponseCodes ResponseStatus { get; set; }

    public string? ResponseMessage { get; set; }
}

public enum GatewayResponseCodes
{
    Ok,
    BadRequest,
    Unauthorized,
    NotFound
}
