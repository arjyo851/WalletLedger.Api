using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Contracts.Requests;
using WalletLedger.Api.Contracts.Responses;

namespace WalletLedger.Api.Controllers
{
    [EnableRateLimiting("UserRateLimit")]
    [ApiController]
    [Route("api/wallets")]
    public class WalletController : ControllerBase
    {

        private readonly IWalletService _walletService;
        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("{walletId}/balance")]
        public async Task<IActionResult> GetBalanceAsync(Guid walletId)
        {
             
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);


            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);
            var balance = await _walletService.GetBalanceAsync(walletId);
            return Ok(new BalanceResponse(balance));
        }

        [Authorize(Policy = "WalletWrite")]
        [HttpPost]
        public async Task<IActionResult> CreateWallet(CreateWalletRequest request)
        {
            // Extracted userId from JWT token for security - users can only create wallets for themselves
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            var walletId = await _walletService.CreateWalletAsync(userId, request.Currency);
            return Ok(new WalletResponse(walletId, request.Currency));

        }
    }
}
