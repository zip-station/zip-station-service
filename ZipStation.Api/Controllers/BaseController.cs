using Microsoft.AspNetCore.Mvc;
using ZipStation.Models.Responses;

namespace ZipStation.Api.Controllers;

public class BaseController : ControllerBase
{
    protected IActionResult ProcessGatewayResponse(GatewayResponse gatewayResponse)
    {
        return gatewayResponse.ResponseStatus switch
        {
            GatewayResponseCodes.Ok => Ok(),
            GatewayResponseCodes.BadRequest => BadRequest(new BadRequestResponse
            {
                Message = gatewayResponse.ResponseMessage
            }),
            GatewayResponseCodes.Unauthorized => Unauthorized(new BadRequestResponse
            {
                Message = gatewayResponse.ResponseMessage ?? "Unauthorized"
            }),
            GatewayResponseCodes.NotFound => NotFound(new BadRequestResponse
            {
                Message = gatewayResponse.ResponseMessage ?? "Not found"
            }),
            _ => StatusCode(500)
        };
    }
}
