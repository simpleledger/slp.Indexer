using Slp.Common.Extensions;
using Slp.Common.Models;
using Slp.Common.Models.Enums;
using Slp.Common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Slp.Common.Utility
{
    public static class TransactionHelpers
    {

        public static IEnumerable<byte> PushData(byte singleByte)
        {
            return PushData(new byte[] { singleByte });
        }
        public static IEnumerable<byte> PushData(byte[] buf)
        {
            if (buf.Length == 0)
                return new byte[] { 0x4C, 0x00 };     //return Buffer.from(Uint8Array.from([0x4C, 0x00]));
            else if (buf.Length < 0x4E)
                return (new byte[] { (byte)buf.Length }).Concat(buf); //return Buffer.concat([Uint8Array.from([buf.length]), buf]);
            else if (buf.Length < 0xFF)
                return (new byte[] { 0x4C, (byte)buf.Length }).Concat(buf); //return Buffer.concat([Uint8Array.from([0x4c, buf.length]), buf]);
            else if (buf.Length < 0xFFFF)
            {
                var lenAs2ByteArray = ((short)buf.Length).ToByteArray(); //tmp.writeUInt16LE(buf.length, 0);
                return (new byte[] { 0x4d }).Concat(lenAs2ByteArray).Concat(buf); //return Buffer.concat([Uint8Array.from([0x4d]), tmp, buf]);
            }
            else if ((uint)buf.Length < 0xFFFFFFFF)
            {
                var lenAs4ByteArray = ((int)buf.Length).ToByteArray(); //tmp.writeUInt32LE(buf.length, 0);
                return (new byte[] { 0x4e }).Concat(lenAs4ByteArray).Concat(buf); //return Buffer.concat([Uint8Array.from([0x4e]), tmp, buf]);
            }
            else
            {
                throw new Exception("PushData OP_RETURN does not support bigger pushes yet");
            }
        }

        internal static NBitcoin.Transaction SimpleTokenSend(
            SlpService slpService,
            string tokenId, decimal[] sendAmounts, AddressUtxoResult[] inputUtxos,
            string[] tokenReceiverAddresses,
            string tokenChangeReceiverAddress,
            string bchChangeReceiverAddress,
            IEnumerable<NonTokenOutput> requiredNonTokenOutputs,
            decimal extraFee)
        {
            if (sendAmounts.Length != tokenReceiverAddresses.Length )
                throw new Exception("Must have send amount item for each token receiver specified.");

            if (string.IsNullOrEmpty(bchChangeReceiverAddress)) //fallback to token change receiver address if no special bch address is specified
                bchChangeReceiverAddress = tokenChangeReceiverAddress;

            // 1) Set the token send amounts, we'll send 100 tokens to a
            //    new receiver and send token change back to the sender
            var totalTokenInputAmount =
                    inputUtxos.Where(txo => SlpService.PreSendSlpJudgementCheck(txo, tokenId))
                    .Sum(s => s.SlpUtxoJudgementAmount.Value);
            //        .filter(txo => {
            //            return Slp.PreSendSlpJudgementCheck(txo, tokenId);
            //        })
            //        .reduce((tot: BigNumber, txo: SlpAddressUtxoResult) => {
            //    return tot.plus(txo.slpUtxoJudgementAmount);
            //}, new BigNumber(0));

            
            // 2) Compute the token Change amount.
            var tokenChangeAmount = totalTokenInputAmount - sendAmounts.Sum();

            // Get token_type
            var slpInput =
                inputUtxos.Where(i =>
                        i.SlpUtxoJudgement == SlpUtxoJudgement.SLP_TOKEN &&
                        i.TransactionDetails.TokenIdHex == tokenId).FirstOrDefault();
            if (slpInput == null)
                throw new Exception($"No valid slp token of type {tokenId} input utxo found.");
             var token_type = slpInput.TransactionDetails.VersionType;

            NBitcoin.Transaction tx=null;
            if (tokenChangeAmount > 0)
            {
                // 3) Create the Send OP_RETURN message
                var outputQtyArray = sendAmounts.Concat(new decimal[] { tokenChangeAmount }).ToArray();
                var sendOpReturn = CreateOpReturnSend(tokenId, outputQtyArray, token_type);
                // 4) Create the raw Send transaction hex
                var inputTokenUtxos = inputUtxos.Select(i => new Utxo
                {
                    Satoshis = i.Satoshis ?? 0,
                    SlpUtxoJudgement = i.SlpUtxoJudgement,
                    SlpUtxoJudgementAmount = i.SlpUtxoJudgementAmount ?? 0,
                    TransactionDetails = i.TransactionDetails,
                    TxId = i.TxId,
                    VOut = i.VOut,
                    Wif = i.Wif
                }).ToArray();
                var configSend = new ConfigBuildRawSendTx()
                {
                    SlpSendOpReturn = sendOpReturn,
                    InputTokenUtxos = inputTokenUtxos,
                    TokenReceiverAddressArray = tokenReceiverAddresses.Concat(new string[] { tokenChangeReceiverAddress }).ToArray(),
                    BchChangeReceiverAddress = bchChangeReceiverAddress,
                    TokenChangeReceiverAddress = tokenChangeReceiverAddress,
                    RequiredNonTokenOutputs = requiredNonTokenOutputs.ToArray(),
                    ExtraFee = extraFee
                };
                tx = slpService.BuildRawSendTx(configSend);
            }
            else if (tokenChangeAmount == 0)
            {
                // 3) Create the Send OP_RETURN message
                var sendOpReturn = CreateOpReturnSend(tokenId, sendAmounts, SlpVersionType.TokenVersionType1);
                // 4) Create the raw Send transaction hex
                var inputTokenUtxos = inputUtxos.Select(i => new Utxo
                {
                    Satoshis = i.Satoshis ?? 0,
                    SlpUtxoJudgement = i.SlpUtxoJudgement,
                    SlpUtxoJudgementAmount = i.SlpUtxoJudgementAmount ?? 0,
                    TransactionDetails = i.TransactionDetails,
                    TxId = i.TxId,
                    VOut = i.VOut,
                    Wif = i.Wif
                }).ToArray();
                var configSend = new ConfigBuildRawSendTx()
                {
                    SlpSendOpReturn = sendOpReturn,
                    InputTokenUtxos = inputTokenUtxos,
                    TokenReceiverAddressArray = tokenReceiverAddresses,
                    TokenChangeReceiverAddress = tokenChangeReceiverAddress,
                    BchChangeReceiverAddress = bchChangeReceiverAddress,
                    RequiredNonTokenOutputs = requiredNonTokenOutputs.ToArray(),
                    ExtraFee = extraFee
                };
                tx = slpService.BuildRawSendTx(configSend);
            }
            else
                throw new Exception("Token inputs less than the token outputs");            
            // Return raw hex for this transaction
            return tx;
        }

        public static byte[] SimpleTokenGenesis(
            SlpVersionType type, 
            string tokenName, 
            string tokenTicker, 
            ulong tokenAmount, 
            string documentUri, 
            string documentHashHex, 
            int decimals,
            string tokenReceiverAddress, 
            string batonReceiverAddress, 
            string bchChangeReceiverAddress,
             AddressUtxoResult[] inputUtxos)
        {
            int? batonVOut = null;
            if (batonReceiverAddress != null)
                batonVOut = 2;
            var genesis = CreateOpReturnGenesis(
                type, tokenTicker, tokenName, 
                documentHashHex, documentUri, decimals,
                batonVOut, tokenAmount);
            return genesis;
            // var rawTx = CreateRa
            // var genesisTxHex = this.
        }


        private static byte[] CreateOpReturnSend(string tokenIdHex, decimal[] slpAmounts, SlpVersionType slpVersionType)
        {
            Regex r = new Regex("^[0-9a-fA-F]{64}$");
            if (!r.IsMatch(tokenIdHex) )
                throw new Exception("tokenIdHex does not pass regex");
            if (tokenIdHex.Length != SD.HashHexSize)
                throw new Exception("tokenIdHex must be 32 bytes");
            var tokenIdHash = tokenIdHex.FromHex();
            if (slpAmounts.Length < 1)
                throw new Exception("send requires at least one amount");
            if (slpAmounts.Length > SD.SlpMaxAllowedOutputs19)
                throw new Exception($"too many slp amounts. Max allowed size is {SD.SlpMaxAllowedOutputs19} got {slpAmounts.Length}");
            var buf = new List<byte>
            {
                SD.OP_RETURN
            };
            buf.AddRange(PushData(SD.SlpLokadIdHex));
            buf.AddRange(PushData((byte)slpVersionType));
            buf.AddRange(PushData(SlpTransactionType.SEND.ToAsciiByteArray()));
            buf.AddRange(PushData(tokenIdHash));
            foreach (var amount in slpAmounts)
                buf.AddRange(PushData(amount.BigNumberToInt64BigEndian()));
            return buf.ToArray();
        }

        private static byte[] CreateOpReturnGenesis(
                  SlpVersionType versionType,
                  string ticker,
                  string name,
                  string documentHash,
                  string documentUrl,
                  int decimals,
                  int? mintBatonVOut,
                  ulong quantity)
        {
            if (decimals < 0 || decimals>9)
                throw new Exception("Decimals property must be in range 0 to 9");
            if (ticker == null)
                throw new Exception("ticker must be a string");
            if (name == null)
                throw new Exception("name must be a string");
            if (documentHash.Length != 0 && documentHash.Length != 64)
                throw new Exception("documentHash must be either 0 or 32 hex bytes");
            Regex r = new Regex("^[0-9a-fA-F]{64}$");
            if (documentHash.Length == 64 && !r.IsMatch(documentHash))
                throw new Exception("documentHash must be hex");
            if (decimals < 0 || decimals > 9)
                throw new Exception("decimals out of range");
            if (mintBatonVOut != null && (mintBatonVOut < 2 || mintBatonVOut > 0xFF))
                throw new Exception("mintBatonVout out of range (0x02 < > 0xFF)");
            if (versionType == SlpVersionType.TokenVersionType1_NFT_Child)
            {
                if (quantity != 1m)
                    throw new Exception("quantity must be 1 for NFT1 child genesis");
                if (decimals != 0)
                    throw new Exception("decimals must be 0 for NFT1 child genesis");
                if (mintBatonVOut != null)
                    throw new Exception("mintBatonVout must be null for NFT1 child genesis");
            }
            var hash = documentHash.FromHex();
            var buf = new List<byte>();
            buf.Add(SD.OP_RETURN);
            buf.AddRange(PushData(SD.SlpLokadIdHex));
            buf.AddRange(PushData((byte)versionType));
            buf.AddRange(PushData(SlpTransactionType.GENESIS.ToAsciiByteArray()));
            buf.AddRange(PushData(ticker.ToAsciiByteArray()));
            buf.AddRange(PushData(name.ToAsciiByteArray()));
            buf.AddRange(PushData(documentUrl.ToAsciiByteArray()));
            buf.AddRange(PushData(hash));
            buf.AddRange(PushData(decimals.ToByteArray()));
            buf.AddRange(PushData(mintBatonVOut.HasValue ? new byte[] { } : new byte[] { (byte)mintBatonVOut }));
            buf.AddRange(PushData(quantity.ToBigEndianByteArray()));
            if( buf.Count > SD.MaxOpReturnSize)
                throw new Exception($"Script too long, must be less than or equal to {SD.MaxOpReturnSize} bytes.");
            return buf.ToArray();
        }
    }
}  
