using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models.Enums
{
    public enum SlpTransactionType
    {
        GENESIS = 1,
        MINT = 2,
        SEND = 3,
        BURN = 4 //this is somehow artifitial slp transaction that does not have op_return bet burns valid slp transaction output( lost tokens)
    }
}
