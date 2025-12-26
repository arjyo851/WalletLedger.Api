using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace WalletLedger.Api.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController:ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok("Admin access verified");
        }
    }
}
