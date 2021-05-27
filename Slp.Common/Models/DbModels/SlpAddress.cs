using Slp.Common.Utility;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    public class SlpAddress
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        [MaxLength(SD.AddressSize)]
        public string Address { get; set; }

        public override bool Equals(object obj)
        {
            if( obj == null)
                return false;
            if (obj is SlpAddress addr )
                return Address == addr.Address;
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
