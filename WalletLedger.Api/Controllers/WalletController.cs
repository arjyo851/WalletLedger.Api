using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Contracts.Requests;
using WalletLedger.Api.Contracts.Responses;

namespace WalletLedger.Api.Controllers
{
    [Authorize(Policy = "UserOnly")]
    [ApiController]
    [Route("api/wallets")]
    public class WalletController : ControllerBase
    {

        private readonly IWalletService _walletService;
        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet("{walletId}/balance")]
        public async Task<IActionResult> GetBalanceAsync(Guid walletId)
        {
            // Try to find userId from NameIdentifier claim (mapped from 'sub' claim)
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);
            var balance = await _walletService.GetBalanceAsync(walletId);
            return Ok(new BalanceResponse(balance));
        }

        [HttpPost]
        public async Task<IActionResult> CreateWallet(CreateWalletRequest request)
        {
            var walletId = await _walletService.CreateWalletAsync(request.UserId, request.Currency);
            return Ok(new WalletResponse(walletId, request.Currency));

        }
    }
}
