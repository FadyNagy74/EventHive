using EventHive.Shared.Common;
using MongoDB.Driver;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IMongoCollection<Notification> _collection;
        private static bool _indexCreated = false;
        private static readonly object _indexLock = new();

        public NotificationRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<Notification>("Notifications");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            if (_indexCreated) return;

            lock (_indexLock)
            {
                if (_indexCreated) return;

                try
                {
                    var messageIdIndex = new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys.Ascending(n => n.MessageId),
                        new CreateIndexOptions { Unique = true, Sparse = true });
                    //Sparse: Only include documents that actually have this field in the index.
                    //Ignore documents where the field is missing
                    //Combined with unique, this allows multiple documents to exist without a
                    //MessageId, but ensures that if a MessageId is present, it must be unique.

                    var bookingIdIndex = new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys.Ascending(n => n.BookingId));

                    var userIdIndex = new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys.Ascending(n => n.UserId));

                    var eventIdIndex = new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys.Ascending(n => n.EventId));

                    _collection.Indexes.CreateMany(new[] { messageIdIndex, bookingIdIndex, userIdIndex, eventIdIndex });
                    _indexCreated = true;
                }
                catch
                {
                    _indexCreated = true;
                }
            }
        }

        public async Task CreateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            await _collection.InsertOneAsync(notification, cancellationToken: cancellationToken);
        }

        public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.Id, notification.Id);
            await _collection.ReplaceOneAsync(filter, notification, cancellationToken: cancellationToken);
        }

        public async Task<bool> DeleteByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId);
            var result = await _collection.DeleteManyAsync(filter, cancellationToken);
            return result.DeletedCount > 0;
        }

        public async Task<Notification?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId);
            return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Notification?> MarkAsSentAsync(Guid bookingId, Guid? messageId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId),
                Builders<Notification>.Filter.Eq(n => n.IsSent, false)
            );

            var update = Builders<Notification>.Update
                .Set(n => n.IsSent, true)
                .Set(n => n.SentAt, DateTime.UtcNow);

            if (messageId.HasValue && messageId.Value != Guid.Empty)
            {
                update = update.Set(n => n.MessageId, messageId.Value);
            }

            return await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        public async Task<Notification?> MarkAsCancelledAsync(Guid bookingId, string reason, Guid? messageId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId),
                Builders<Notification>.Filter.Eq(n => n.IsSent, false)
            );

            var failureMessage = string.IsNullOrWhiteSpace(reason)
                ? "Your booking could not be completed due to a payment failure. Please retry your booking."
                : $"Your booking could not be completed due to a payment failure: {reason}. Please retry your booking.";

            var update = Builders<Notification>.Update
                .Set(n => n.Type, "PaymentFailed")
                .Set(n => n.Message, failureMessage)
                .Set(n => n.IsSent, true)
                .Set(n => n.SentAt, DateTime.UtcNow);

            if (messageId.HasValue && messageId.Value != Guid.Empty)
            {
                update = update.Set(n => n.MessageId, messageId.Value);
            }

            return await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        public async Task<Notification?> MarkAsExpiredAsync(Guid bookingId, Guid? messageId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId),
                Builders<Notification>.Filter.Eq(n => n.IsSent, false)
            );

            var expiryMessage = "Your booking reservation has expired because payment was not completed in time. Your held seats have been released.";

            var update = Builders<Notification>.Update
                .Set(n => n.Type, "BookingExpired")
                .Set(n => n.Message, expiryMessage)
                .Set(n => n.IsSent, true)
                .Set(n => n.SentAt, DateTime.UtcNow);

            if (messageId.HasValue && messageId.Value != Guid.Empty)
            {
                update = update.Set(n => n.MessageId, messageId.Value);
            }

            return await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        public async Task<Notification?> MarkAsEventCancelledAsync(Guid id, string eventName, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.Id, id),
                Builders<Notification>.Filter.Eq(n => n.IsSent, false)
            );

            var message = $"The event '{eventName}' has been cancelled and your booking has been voided.";

            var update = Builders<Notification>.Update
                .Set(n => n.Type, "EventCancelled")
                .Set(n => n.Message, message)
                .Set(n => n.IsSent, true)
                .Set(n => n.SentAt, DateTime.UtcNow);

            return await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        public async Task<List<Notification>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.EventId, eventId);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByBookingIdAndTypeAsync(Guid bookingId, string type, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.BookingId, bookingId),
                Builders<Notification>.Filter.Eq(n => n.Type, type)
            );
            return await _collection.Find(filter).AnyAsync(cancellationToken);
        }

        public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PagedResponse<Notification>> GetByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.UserId, userId),
                Builders<Notification>.Filter.Eq(n => n.IsSent, true)
            );

            var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            var items = await _collection
                .Find(filter)
                .SortByDescending(n => n.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<Notification>
            {
                Items = items,
                TotalCount = (int)totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
