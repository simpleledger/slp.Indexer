using Slp.Common.Models.DbModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Slp.Common.Utility;

namespace Slp.Common.DataAccess
{
    public class SlpDbInitializer : ISlpDbInitializer
    {
        private readonly SlpDbContext _db;
        private readonly ILogger<SlpDbInitializer> _log;
        private readonly IConfiguration _configuration;
        public SlpDbInitializer(SlpDbContext db, IConfiguration configuration, ILogger<SlpDbInitializer> log)
        {
            _db = db;
            _log = log;
            _configuration = configuration;
        }
        
        public void Initialize()
        {
            var timeout= _db.Database.GetCommandTimeout();
            try
            {
                _log.LogInformation("Initializing slp database...");
                _db.Database.SetCommandTimeout(3600);
                if (_db.Database.GetPendingMigrations().Any())
                {
                    _log.LogInformation("Applying slp database migration to latest...");
                    _db.Database.Migrate();
                    _log.LogInformation("All new slp migrations have been applied.");
                }
            }
            catch (Exception ex)
            {
                _log.LogError("Failed to perform db migration: {0}", ex.Message);
                throw;
            }
            finally
            {
                _db.Database.SetCommandTimeout(timeout);
            }
            if (!_db.SlpDatabaseState.Any())
            {
                var blockTip = _configuration.GetValue<int>(nameof(SD.StartFromBlock), 0);
                _db.SlpDatabaseState.Add(
                    new DatabaseState
                    {
                        Id = 1,
                        BlockTip = blockTip, //block before first slp transaction
                        BlockTipHash = null,
                        LastStatusUpdate = null
                    }
                    );
                _db.SaveChanges();
            }           
        }
    }
}
