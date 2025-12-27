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
    [Route("api/transactions")]
    public class TransactionController : ControllerBase
    {

        private readonly ILedgerService _ledgerService;
        private readonly IWalletService _walletService;

        public TransactionController(ILedgerService ledgerService, IWalletService walletService)
        {
            _ledgerService = ledgerService;
            _walletService = walletService;
        }

        [Authorize(Policy = "TransactionCredit")]
        [HttpPost("credit")]
        public async Task<IActionResult> Credit(TransactionRequest request)
        {
            await _ledgerService.CreditAsync(request.WalletId, request.Amount, request.ReferenceId);
            return Ok();
        }

        [Authorize(Policy = "TransactionDebit")]
        [HttpPost("debit")]
        public async Task<IActionResult> Debit(TransactionRequest request)
        {
            await _ledgerService.DebitAsync(request.WalletId, request.Amount, request.ReferenceId);
            return Ok();
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("history")]
        public async Task<IActionResult> GetTransactionHistory([FromQuery] TransactionHistoryRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(request.WalletId, userId);

            var history = await _ledgerService.GetTransactionHistoryAsync(
                request.WalletId,
                request.PageNumber,
                request.PageSize,
                request.StartDate,
                request.EndDate,
                request.Type,
                request.MinAmount,
                request.MaxAmount,
                request.SortBy ?? "CreatedAt",
                request.SortOrder ?? "desc"
            );

            return Ok(history);
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("{transactionId}")]
        public async Task<IActionResult> GetTransactionById(Guid transactionId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            var transaction = await _ledgerService.GetTransactionByIdAsync(transactionId);

            if (transaction == null)
            {
                return NotFound("Transaction not found");
            }

            await _walletService.ValidateWalletOwnership(transaction.WalletId, userId);

            return Ok(transaction);
        }

        [Authorize(Policy = "WalletRead")]
        [HttpGet("by-reference/{walletId}/{referenceId}")]
        public async Task<IActionResult> GetTransactionByReferenceId(Guid walletId, string referenceId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            await _walletService.ValidateWalletOwnership(walletId, userId);

            var transaction = await _ledgerService.GetTransactionByReferenceIdAsync(walletId, referenceId);

            if (transaction == null)
            {
                return NotFound("Transaction not found");
            }

            return Ok(transaction);
        }

        [Authorize(Policy = "TransactionDebit")]
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer(TransferRequest request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("User ID claim not found in token");
            }

            var userId = Guid.Parse(userIdClaim);

            // Validate ownership of source wallet
            await _walletService.ValidateWalletOwnership(request.FromWalletId, userId);

            var transfer = await _ledgerService.TransferBetweenWalletsAsync(
                request.FromWalletId,
                request.ToWalletId,
                request.Amount,
                request.ReferenceId
            );

            return Ok(transfer);
        }


    }
}
