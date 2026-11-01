using MongoDB.Bson.Serialization.Attributes;

namespace NotificationService.Domain.Entities
{
    public class Notification
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BookingId { get; set; }

        public Guid EventId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Channel { get; set; } = string.Empty;

        public Guid MessageId { get; set; }

        public bool IsSent { get; set; } = false;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
