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

        [BsonElement("LecturerName")]
        public string? Name { get; set; }

        [JsonPropertyName("Email")]
        [BsonElement("Lecturer_Email")]
        public string Email { get; set; } = string.Empty;

        // NEW: match your collection fields
        [JsonPropertyName("FirstName")]
        [BsonElement("LecturerFirstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("LastName")]
        [BsonElement("LecturerLastName")]
        public string? LastName { get; set; }
    }
}
