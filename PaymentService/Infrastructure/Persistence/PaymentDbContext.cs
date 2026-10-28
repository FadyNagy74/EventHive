using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence
{
    public class PaymentDbContext : DbContext
    {
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
       : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(p => p.BookingId)
                    .IsRequired();

                entity.HasIndex(p => p.BookingId)
                    .IsUnique()
                    .HasDatabaseName("UQ_Payments_BookingId");

                entity.Property(p => p.UserId)
                    .IsRequired();

                entity.HasIndex(p => p.UserId)
                    .HasDatabaseName("IX_Payments_UserId");

                entity.Property(p => p.ExternalOrderId)
                    .HasMaxLength(100);

                entity.HasIndex(p => p.ExternalOrderId)
                    .IsUnique()
                    .HasDatabaseName("UQ_Payments_ExternalOrderId");

                entity.Property(p => p.ClientSecret)
                    .HasMaxLength(200);

                entity.Property(p => p.CheckoutUrl)
                    .HasMaxLength(1000);

                entity.Property(p => p.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasMany(p => p.Transactions)
                    .WithOne(t => t.RelatedPayment)
                    .HasForeignKey(t => t.PaymentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(p => p.Amount)
                    .IsRequired()
                    .HasColumnType("decimal(18,2)");

            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(t => t.PaymentId)
                    .IsRequired();

                entity.Property(p => p.ExternalTransactionId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(p => p.ExternalTransactionId)
                    .IsUnique()
                    .HasDatabaseName("UQ_Transactions_ExternalTransactionId");

                entity.Property(p => p.Status)
                    .IsRequired()
                    .HasMaxLength(50);
            });
        }

    }
}
