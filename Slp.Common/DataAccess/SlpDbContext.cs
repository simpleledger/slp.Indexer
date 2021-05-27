using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using Microsoft.EntityFrameworkCore;

namespace Slp.Common.DataAccess
{
    public partial class SlpDbContext : DbContext
    {
        //we can have central id manager that is sync with database whenever we need to insert new records
        public DbSlpIdManager DbSlpIdManager { get; set; }
        public static SlpDbContext FromConnectionString(string connectionString ) 
        {
            var optBuilder = new DbContextOptionsBuilder<SlpDbContext>();
            optBuilder.UseSqlServer(connectionString);
            return new SlpDbContext(optBuilder.Options);
        }
        public SlpDbContext(DbContextOptions<SlpDbContext> options)
            : base(options)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SlpBlock>()
                .HasIndex(i => i.Hash);
            modelBuilder.Entity<SlpTransaction>()
                .HasIndex(i => i.Hash);
            //modelBuilder.Entity<SlpTransaction>()
            //    .HasIndex(i => i.BlockHeight);
            //modelBuilder.Entity<SlpTransactionInput>()
            //    .HasIndex(i => i.SlpSourceTransactionHex);
            modelBuilder.Entity<SlpAddress>()
                .HasIndex(i => i.Address)
                .IsUnique();
            //modelBuilder.Entity<SlpTransactionInput>()
            //    .HasIndex(i => i.Address);
            //modelBuilder.Entity<SlpTransactionOutput>()
            //    .HasIndex(i => i.SlpTransactionHex);
            //modelBuilder.Entity<SlpTransactionOutput>()
            //    .HasIndex(i => i.Address);
        }
        // SLP
        public DbSet<SlpBlock> SlpBlock { get; set; }
        public DbSet<SlpToken> SlpToken { get; set; }
        public DbSet<SlpAddress> SlpAddress { get; set; }
        public DbSet<SlpTransaction> SlpTransaction { get; set; }
        public DbSet<SlpTransactionInput> SlpTransactionInput { get; set; }
        public DbSet<SlpTransactionOutput> SlpTransactionOutput { get; set; }
        public DbSet<DatabaseState> SlpDatabaseState { get; set; }
    }
}
