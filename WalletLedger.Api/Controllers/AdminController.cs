using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace WalletLedger.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController:ControllerBase
    {
        [Authorize(Policy = "AdminHealth")]
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok("Admin access verified");
        }
    }
}
