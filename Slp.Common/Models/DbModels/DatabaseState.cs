using Slp.Common.Utility;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Slp.Common.Models.DbModels
{
    public class DatabaseState
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public DateTime? LastStatusUpdate { get; set; }
        /// <summary>
        /// This is used for syncing. Value is updated before batch is being written into db
        /// This way if anything goes wrong we restart sync from this block height and delete all data after this height
        /// </summary>
        /// <summary>
        /// Whenever block is processed and stored into database tip
        /// </summary>
        public int BlockTip { get; set; }
        [MaxLength(SD.HashHexSize)]
        public string BlockTipHash { get; set; }
    }
}
