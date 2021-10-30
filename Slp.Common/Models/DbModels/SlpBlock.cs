using Slp.Common.Utility;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    /// <summary>
    /// This table contains information about block
    /// </summary>
    public class SlpBlock
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Height { get; set; }
        [MaxLength(SD.HashSize)]
        public byte[] Hash { get; set; }        
        public DateTime BlockTime { get; set; }        
        public bool IsSlp { get; set; }
    }    
}
