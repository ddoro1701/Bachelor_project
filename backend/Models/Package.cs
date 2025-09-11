using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplication1.Models
{
    public class Package
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? LecturerEmail { get; set; }
        public string? LecturerFirstName { get; set; }
        public string? LecturerLastName { get; set; }

        public int ItemCount { get; set; }
        public string? ShippingProvider { get; set; }
        public string? AdditionalInfo { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? CollectionDate { get; set; }

        public string? Status { get; set; }
        public string? ImageUrl { get; set; }

        // QR
        [BsonElement("QrToken")]
        public string? QrToken { get; set; }

        [BsonElement("QrExpiresAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrExpiresAt { get; set; }

        [BsonElement("QrUsedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? QrUsedAt { get; set; }

        // PIN (falls genutzt)
        [BsonElement("PinCode")]
        public string? PinCode { get; set; }

        [BsonElement("PinExpiresAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? PinExpiresAt { get; set; }

        [BsonElement("PinUsedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? PinUsedAt { get; set; }
    }
}