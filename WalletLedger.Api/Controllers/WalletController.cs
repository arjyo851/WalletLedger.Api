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

        [Authorize(Policy = "WalletRead")]
        [HttpGet]
        public async Task<IActionResult> GetUserWallets([FromQuery] string? currency = null)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            var wallets = await _walletService.GetUserWalletsAsync(userId, currency);
            return Ok(wallets);
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("{walletId}")]
        public async Task<IActionResult> GetWalletDetails(Guid walletId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);

            var wallet = await _walletService.GetWalletDetailsAsync(walletId);

            if (wallet == null)
            {
                return NotFound("Wallet not found");
            }

            return Ok(wallet);
        }

        [Authorize(Policy = "WalletWrite")]
        [HttpPut("{walletId}/status")]
        public async Task<IActionResult> UpdateWalletStatus(Guid walletId, UpdateWalletStatusRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.UpdateWalletStatusAsync(walletId, userId, request.Status);
            return Ok();
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("{walletId}/balance/point-in-time")]
        public async Task<IActionResult> GetBalanceAtPointInTime(Guid walletId, [FromQuery] PointInTimeBalanceRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);

            var balance = await _walletService.GetBalanceAtPointInTimeAsync(walletId, request.AsOfDate);
            return Ok(new BalanceResponse(balance));
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("{walletId}/balance/history")]
        public async Task<IActionResult> GetBalanceHistory(Guid walletId, [FromQuery] BalanceHistoryRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);

            var history = await _walletService.GetBalanceHistoryAsync(
                walletId,
                request.StartDate,
                request.EndDate,
                request.PageNumber,
                request.PageSize
            );

            return Ok(history);
        }

        [Authorize(Policy = "WalletWrite")]
        [HttpPost("{walletId}/balance/snapshot")]
        public async Task<IActionResult> CreateBalanceSnapshot(Guid walletId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);
            await _walletService.CreateBalanceSnapshotAsync(walletId);
            return Ok();
        }
    }
}
