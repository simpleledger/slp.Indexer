using System.Numerics;

namespace Slp.Common.Models
{
    public class ConfigBuildMintOpReturn
    {
        public string TokenIdHex { get; set; }
        public decimal? BatonVOut { get; set; }
        public BigInteger MintQuantity { get; set; }
    }
}
