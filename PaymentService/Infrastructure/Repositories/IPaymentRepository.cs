using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByBookingIdAsync(Guid bookingId);
        Task<Payment?> GetByExternalOrderIdAsync(string externalOrderId);
        Task<bool> TransactionExistsAsync(string externalTransactionId);
        Task CreateAsync(Payment payment);
        Task UpdateAsync(Payment payment);
        Task AddTransactionAsync(Transaction transaction);
    }
}
