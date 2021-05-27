using Slp.API.Models;
using Slp.Common.Extensions;
using Slp.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Slp.Common.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Slp.API.Controllers
{
    [Produces("application/json")]
    [Route("slp")]
    [ApiController]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Input data validation failed.", Type = typeof(ErrorDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Internal server error.")]
    public class SlpControllerV2 : Controller
    {
        private readonly ISlpDataService _slpDataService;
        private readonly ILogger<SlpControllerV2> _log;
        public SlpControllerV2(
            ISlpDataService slpService,
            ILogger<SlpControllerV2> log)
        {
            _slpDataService = slpService;
            _log = log;
        }


        [HttpGet]
        [Route("list")]
        [SwaggerOperation(Summary = "list token", Description = "List single SLP token by id")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Token information returned succesfully.", type: typeof(TokenViewModel))]
        public async Task<IActionResult> ListTokenData(
            [SwaggerParameter(Required = true)] 
            [DefaultValue("259908ae44f46ef585edef4bcc1e50dc06e4c391ac4be929fae27235b8158cf1")]
            string tokenId)
        {
            var res = await _slpDataService.GetTokenInformationAsync(new string[] { tokenId });
            var tinfo = new TokenViewModel();
            if (res.Any())
                tinfo = res.First();
            return Ok(tinfo);
        }

        [HttpPost]
        [Route("list")]
        [SwaggerOperation(Summary = "list tokens information", Description = "List multiple SLP tokens by id")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Token information array returned succesfully.", type: typeof(TokenViewModel[]))]
        public async Task<IActionResult> ListTokenData(
            [SwaggerParameter(Required = true)] TokenIds tokenIds)
        {
            var res = await _slpDataService.GetTokenInformationAsync(tokenIds.tokenIds);
            return Ok(res); 
        }

        [HttpGet]
        [Route("convert")]
        [SwaggerOperation(Summary = "convert address to slpAddr, cashAddr and legacy", Description = "convert address to slpAddr, cashAddr and legacy")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address variants returned", type: typeof(AddressInfo))]
        public IActionResult ConvertAddress(
            [SwaggerParameter(Required = true)]
            string address)
        {
            var addressData = address.DecodePrefixedBCashOrSlpAddress();
            var res = new AddressInfo()
            {
                SlpAddress = address.ToPrefixedSlpAddress(),
                CashAddress = address.ToPrefixedBchAddress(),
                LegacyAddress = address.ToLegacyAddress()
            };
            return Ok(res);
        }

        [HttpPost]
        [Route("convert")]
        [SwaggerOperation(Summary = "convert address to slpAddr, cashAddr and legacy", Description = "convert address to slpAddr, cashAddr and legacy")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address variants array returned", type: typeof(AddressInfo[]))]
        public IActionResult ConvertAddresses(
            [SwaggerParameter(Required = true)] AddressArray addresses)
        {
            List<AddressInfo> res = new List<AddressInfo>();
            foreach (var address in addresses.Addresses)
            {
                var addressData = address.DecodePrefixedBCashOrSlpAddress();
                res.Add(new AddressInfo()
                {
                    SlpAddress = address.ToPrefixedSlpAddress(),
                    CashAddress = address.ToPrefixedBchAddress(),
                    LegacyAddress = address.ToLegacyAddress()
                });

            }
            return Ok(res.ToArray());
        }


        [HttpGet]
        [Route("balancesForAddress/{address}")]
        [SwaggerOperation(Summary = "list slp balances for single address", Description = "List SLP token balances for single address")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address variants returned", type: typeof(AddressBalance[]))]
        public async Task<IActionResult> BalancesForAddress(
            [SwaggerParameter(Required = true)]
            [DefaultValue("simpleledger:qz9tzs6d5097ejpg279rg0rnlhz546q4fsnck9wh5m")]
            string address)
        {
            var res = await _slpDataService.GetAddressBalancesAsync(address);
            return Ok(res);
        }


        [HttpPost]
        [Route("balancesForAddress")]
        [SwaggerOperation(Summary = "list slp balances for bulk addresses", Description = "List SLP token balances for bulk addresses")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address variants returned", type: typeof(AddressBalance[][]))]
        public async Task<IActionResult> BalancesForAddress(
            [SwaggerParameter(Required = true)] AddressArray addresses)
        {
            var res = new List<AddressBalance[]>();
            foreach (var a in addresses.Addresses)
            {
                var addressBalances = await _slpDataService.GetAddressBalancesAsync(a);
                res.Add(addressBalances);
            }            
            return Ok(res.ToArray());
        }

        [HttpGet]
        [Route("balancesForToken/{tokenId}")]
        [SwaggerOperation(Summary = "list slp balances for single token", Description = "List SLP token balances for single token")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Token balances returned", type: typeof(TokenBalanceData[]))]
        public async Task<IActionResult> BalancesForToken(
            [SwaggerParameter(Required = true)]
            [DefaultValue("df808a41672a0a0ae6475b44f272a107bc9961b90f29dc918d71301f24fe92fb")]
            string tokenId)

        {
            var res = await _slpDataService.GetTokenBalancesAsync(tokenId);
            return Ok(res);
        }

        [HttpPost]
        [Route("balancesForToken")]
        [SwaggerOperation(Summary = "list SLP addresses and balances for bulk tokenIds", Description = "List SLP addresses and balances for bulk tokenIds")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Multiple token balances returned", type: typeof(TokenBalanceData[][]))]
        public async Task<IActionResult> BulkBalancesForToken(
            [SwaggerParameter(Required = true)] TokenIds tokenIds)
        {
            var res = new List<TokenBalanceData[]>();
            foreach (var tid in tokenIds.tokenIds)
            {
                var tokenBalances = await _slpDataService.GetTokenBalancesAsync(tid);
                res.Add(tokenBalances);
            }
            return Ok(res.ToArray());
        }


        [HttpGet]
        [Route("balance/{address}/{tokenId}")]
        [SwaggerOperation(Summary = "list single slp token balance for address", Description = "List single slp token balance for address")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address token balance returned", type: typeof(AddressBalance))]
        public async Task<IActionResult> AddressTokenBalance(
           [SwaggerParameter(Required = true)]
           [DefaultValue("simpleledger:qz9tzs6d5097ejpg279rg0rnlhz546q4fsnck9wh5m")]
           string address,
           [SwaggerParameter(Required = true)]
           [DefaultValue("1cda254d0a995c713b7955298ed246822bee487458cd9747a91d9e81d9d28125")]
            string tokenId
           )
        {
            var tokenBalance = await _slpDataService.GetTokenBalancesAsync(address, tokenId);
            return Ok(tokenBalance);
        }

        [HttpPost]
        [Route("balance")]
        [SwaggerOperation(Summary = "list bulk slp token balances for address", Description = "list bulk slp token balances for address")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Address token balance returned", type: typeof(AddressBalance[]))]
        public async Task<IActionResult> BulkAddressTokenBalance(
          [SwaggerParameter(Required = true)] TokenAddress[] tokenAddresses
          )
        {
            var res = new List<AddressBalance>();
            foreach (var tid in tokenAddresses)
            {
                var tokenBalance = await _slpDataService.GetTokenBalancesAsync(tid.Address, tid.TokenId); //TODO: single query
                res.Add(tokenBalance);
            }
            return Ok(res.ToArray());
        }

        [HttpGet]
        [Route("validateTxid/{txid}")]
        [SwaggerOperation(Summary = "Validate single txid", Description = "Validate single txid")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Validation result returned", type: typeof(ValidationResult))]
        public async Task<IActionResult> ValidateTransaction(
          [SwaggerParameter(Required = true)]
          [DefaultValue("1cda254d0a995c713b7955298ed246822bee487458cd9747a91d9e81d9d28125")]
            string txid
          )
        {
            var res = await _slpDataService.ValidateTransactionAsync(txid);
            return Ok(res);
        }

        [HttpPost]
        [Route("validateTxid")]
        [SwaggerOperation(Summary = "Validate multiple txid", Description = "Validate multiple txid")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Validation result returned", type: typeof(ValidationResult[]))]
        public async Task<IActionResult> ValidateTransaction(
         [SwaggerParameter(Required = true)] TxIds txIds
         )
        {
            var res = await _slpDataService.ValidateTransactionsAsync(txIds);
            return Ok(res);
        }


        [HttpGet]
        [Route("tokenStats/{tokenId}")]
        [SwaggerOperation(Summary = "List stats for a single slp token", Description = "List stats for a single slp token")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Token stats returned", type: typeof(TokenViewModel))]
        public async Task<IActionResult> TokenStats(
         [SwaggerParameter(Required = true)]
         [DefaultValue("df808a41672a0a0ae6475b44f272a107bc9961b90f29dc918d71301f24fe92fb")]
            string tokenId
         )
        {
            var res = await _slpDataService.GetTokenInformationAsync(new string[] { tokenId });
            var tokenStats = res.First();
            return Ok(tokenStats);
        }

        [HttpPost]
        [Route("tokenStats")]
        [SwaggerOperation(Summary = "List stats for a multiple slp token", Description = "List stats for a multiple slp token")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Token stats returned", type: typeof(TokenViewModel[]))]
        public async Task<IActionResult> BulkTokenStats(
         [SwaggerParameter(Required = true)] TokenIds tokenIds
         )
        {
            var res = await _slpDataService.GetTokenInformationAsync(tokenIds.tokenIds);
            return Ok(res);
        }


        [HttpGet]
        [Route("txDetails/{txid}")]
        [SwaggerOperation(Summary = "SLP transaction details", Description = "Transaction details on a token transfer.")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Slp transaction details returned", type: typeof(TxDetails))]
        public async Task<IActionResult> TxDetails(
            [SwaggerParameter(Required = true)]
            [DefaultValue("8ab4ac5dea3f9024e3954ee5b61452955d659a34561f79ef62ac44e133d0980e")]
            string txid)
        {
            var res = await _slpDataService.GetTransactionDetails(txid);
            return Ok(res);
        }

        [HttpGet]
        [Route("transactions/{tokenId}/{address}")]
        [SwaggerOperation(Summary = "SLP transactions by tokenId and address", Description = "SLP transactions by tokenId and address")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Slp transactions info", type: typeof(TxTokenDetails[]))]
        public async Task<IActionResult> TxsByTokenAndAddress(
            [SwaggerParameter(Required = true)]
            [DefaultValue("495322b37d6b2eae81f045eda612b95870a0c2b6069c58f70cf8ef4e6a9fd43a")]
            string tokenId,
            [SwaggerParameter(Required = true)]
            [DefaultValue("simpleledger:qrhvcy5xlegs858fjqf8ssl6a4f7wpstaqnt0wauwu")]
            string address
            )
        {
            var res = await _slpDataService.GetTransactions(tokenId, address);
            return Ok(res);
        }

        [HttpGet]
        [Route("transactions")]
        [SwaggerOperation(Summary = "Bulk SLP transactions by tokenId and address", Description = "Bulk SLP transactions by tokenId and address")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Slp transactions info", type: typeof(TxTokenDetails[][]))]
        public async Task<IActionResult> BulkTxsByTokenAndAddress(
            [SwaggerParameter(Required = true)] TokenAddress[] tokenAddresses
            )
        {
            var res = new List<TxTokenDetails[]>();
            foreach (var ta in tokenAddresses)
            {
                var taTxs = await _slpDataService.GetTransactions(ta.TokenId, ta.Address);
                res.Add(taTxs);
            }            
            return Ok(res);
        }

        [HttpGet]
        [Route("burnTotal/{transactionId}")]
        [SwaggerOperation(Summary = "Total burn count for slp transaction", Description = "total input, ouput and burn counts by transaction Id.")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Slp transaction burn info", type: typeof(TxBurn))]
        public async Task<IActionResult> BurnTotal(
            [SwaggerParameter(Required = true)]
            [DefaultValue("c7078a6c7400518a513a0bde1f4158cf740d08d3b5bfb19aa7b6657e2f4160de")]
            string transactionId
            )
        {
            var res = await _slpDataService.GetBurnTotal(new string[] { transactionId });
            return Ok(res.First());
        }


        [HttpPost]
        [Route("burnTotal")]
        [SwaggerOperation(Summary = "Total burn count for slp transactions", Description = "total input, ouput and burn counts by transaction Ids.")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Slp transaction burn infos", type: typeof(TxBurn[]))]
        public async Task<IActionResult> BulkBurnTotal(
            [SwaggerParameter(Required = true)]
            TxIds txIds
            )
        {
            
            var res = await _slpDataService.GetBurnTotal(txIds.txids);
            return Ok(res);
        }
    }
}
