using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure.Persistence
{
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
       : base(options) { }

        public DbSet<Event> Events { get; set; }
        public DbSet<ManualCapacityAdjustment> ManualCapacityAdjustments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Event>(builder =>
            {
                builder.HasKey(e => e.Id);

                builder.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(1000);

                builder.Property(e => e.Venue)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(e => e.Price)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)");

                builder.Property(e => e.Status).IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50);

                builder.HasMany(e => e.CapacityAdjustments)
                    .WithOne(a => a.Event)
                    .HasForeignKey(a => a.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Event>()
        .HasIndex(e => new { e.Name, e.Venue, e.Date })
        .IsUnique()
        .HasDatabaseName("IX_Events_Name_Venue_Date");

            modelBuilder.Entity<ManualCapacityAdjustment>(builder =>
            {
                builder.HasKey(a => a.Id);

                builder.Property(a => a.Reason)
                    .IsRequired()
                    .HasMaxLength(500);

                builder.Property(a => a.AdjustedBy)
                    .IsRequired()
                    .HasMaxLength(200);
            });
        }
    }
}
