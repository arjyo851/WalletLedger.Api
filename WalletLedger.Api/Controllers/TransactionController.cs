using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletLedger.Api.Application.Interfaces;
using WalletLedger.Api.Contracts;
using WalletLedger.Api.Contracts.Requests;

namespace WalletLedger.Api.Controllers
{
    [ApiController]
    [Route("api/transactions")]
    public class TransactionController : ControllerBase
    {

        private readonly ILedgerService _ledgerService;
        public TransactionController(ILedgerService ledgerService)
        {
            _ledgerService = ledgerService;
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


    }
}
