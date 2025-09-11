using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WebApplication1.Models
{
    public class Lecturer
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Legacy full name (kept for back-compat)
        [BsonElement("LecturerName")]
        public string? Name { get; set; }

        [BsonElement("LecturerFirstName")]
        public string? FirstName { get; set; }

        [BsonElement("LecturerLastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("Email")]
        [BsonElement("Lecturer_Email")]
        public string Email { get; set; } = string.Empty;

        [BsonIgnore]
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Name)
                ? Name!
                : $"{FirstName} {LastName}".Trim();
    }
}
