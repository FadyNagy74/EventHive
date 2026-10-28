using System.Collections;

namespace PaymentService.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BookingId { get; set; }
        public Guid UserId { get; set; }
        public string? ExternalOrderId { get; set; }
        public string? ClientSecret { get; set; }
        public string? CheckoutUrl { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

