using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class LecturerService
    {
        private readonly IMongoCollection<Lecturer> _lecturers;

        public LecturerService(IConfiguration configuration)
        {
            string connectionString = configuration["COSMOS_CONNECTION_STRING"]
                ?? throw new Exception("COSMOS_CONNECTION_STRING is not set.");
            string databaseName = configuration["COSMOS_DATABASE_NAME"]
                ?? throw new Exception("COSMOS_DATABASE_NAME is not set.");
            string collectionName = configuration["COSMOS_LECTURER_COLLECTION_NAME"] ?? "Lecturers";

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _lecturers = database.GetCollection<Lecturer>(collectionName);
        }

        public async Task<List<Lecturer>> GetAllLecturersAsync()
        {
            return await _lecturers.Find(FilterDefinition<Lecturer>.Empty).ToListAsync();
        }

        public async Task<Lecturer?> GetByEmailAsync(string email)
        {
            return await _lecturers.Find(l => l.Email == email).FirstOrDefaultAsync();
        }

        public async Task UpsertAsync(Lecturer lecturer)
        {
            var filter = Builders<Lecturer>.Filter.Eq(x => x.Email, lecturer.Email);
            var update = Builders<Lecturer>.Update
                .Set(x => x.Email, lecturer.Email)
                .Set(x => x.FirstName, lecturer.FirstName)
                .Set(x => x.LastName, lecturer.LastName);
            await _lecturers.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<bool> DeleteByEmailAsync(string email)
        {
            var res = await _lecturers.DeleteOneAsync(l => l.Email == email);
            return res.DeletedCount > 0;
        }

        // Helper to split a legacy full name when creating lecturers
        public static (string? first, string? last) SplitFullName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return (parts[0], null);
            var last = parts[^1];
            var first = string.Join(' ', parts[..^1]);
            return (first, last);
        }
    }
}