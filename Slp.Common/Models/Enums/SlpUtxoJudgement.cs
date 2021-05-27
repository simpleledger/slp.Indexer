using System;
using System.Collections.Generic;
using System.Text;

namespace Slp.Common.Models.Enums
{
    public enum SlpUtxoJudgement
    {
        UNSUPPORTED_TYPE    = 0,
        UNKNOWN             = 1,
        INVALID_BATON_DAG   = 2,
        INVALID_TOKEN_DAG   = 3,
        NOT_SLP             = 4,
        SLP_TOKEN           = 5,
        SLP_BATON           = 6
    }
}
