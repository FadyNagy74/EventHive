namespace PaymentService.Domain.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ExternalTransactionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Payment? RelatedPayment { get; set; }
    }
}
