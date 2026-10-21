using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Persistence
{
    public class BookingDbContext : DbContext
    {
        public BookingDbContext(DbContextOptions<BookingDbContext> options)
       : base(options) { }

        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Booking>(builder =>
            {
                builder.HasKey(e => e.Id);

                builder.Property(b => b.EventId)
                .IsRequired();

                builder.Property(b => b.UserId)
                    .IsRequired();

                builder.HasIndex(b => new { b.UserId, b.Status })
                .HasDatabaseName("IX_Bookings_UserId_Status");

                builder.HasIndex(b => new { b.UserId, b.EventId })
                    .HasFilter("[Status] = 'Pending'")
                    .IsUnique()
                    .HasDatabaseName("UQ_Bookings_UserId_EventId_Pending");

                builder.Property(b => b.Quantity)
                .IsRequired();

                builder.ToTable(t => t.HasCheckConstraint(
                "CK_Bookings_Quantity_Positive",
                "[Quantity] > 0"));

                builder.Property(b => b.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

                builder.Property(b => b.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

                builder.Property(b => b.UpdatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                builder.Property(e => e.TotalPrice)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)");
            });
        }
    }
}
