using MongoDB.Bson;
using MongoDB.Driver;
using WebApplication1.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class PackageService
    {
        private readonly IMongoCollection<Package> _packages;

        public PackageService(IConfiguration configuration)
        {
            var databaseName =
                configuration["COSMOS_DATABASE_NAME"]
                ?? configuration["CosmosDb:DatabaseName"]
                ?? throw new Exception("COSMOS_DATABASE_NAME/CosmosDb:DatabaseName is not set.");

            var collectionName =
                configuration["COSMOS_COLLECTION_NAME"]
                ?? "Packages";

            var conn = configuration["COSMOS_CONNECTION_STRING"];
            if (string.IsNullOrWhiteSpace(conn))
            {
                // Build from COSMOS_ACCOUNT/COSMOS_KEY or CosmosDb:Account/Key
                var accountRaw = configuration["COSMOS_ACCOUNT"] ?? configuration["CosmosDb:Account"]
                                 ?? throw new Exception("Set COSMOS_CONNECTION_STRING or COSMOS_ACCOUNT/COSMOS_KEY.");
                var key = configuration["COSMOS_KEY"] ?? configuration["CosmosDb:Key"]
                          ?? throw new Exception("Set COSMOS_KEY/CosmosDb:Key.");

                // accountName aus FQDN extrahieren
                var host = accountRaw.Replace("https://", "")
                                     .Replace("mongodb://", "")
                                     .Trim()
                                     .TrimEnd('/');
                // host kann z.B. "wrexhamuni-ocr-webapp-server.documents.azure.com" oder nur "wrexhamuni-ocr-webapp-server" sein
                var accountName = host.Split('.').First();

                if (host.Contains("documents.azure.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Legacy (Mongo v3.x) Verbindungszeichenfolge
                    conn =
                        $"mongodb://{Uri.EscapeDataString(accountName)}:{Uri.EscapeDataString(key)}@" +
                        $"{accountName}.documents.azure.com:10255/?ssl=true&replicaSet=globaldb&retrywrites=false&maxIdleTimeMS=120000&appName=@{accountName}@";
                }
                else
                {
                    // vCore (SRV)
                    conn =
                        $"mongodb+srv://{Uri.EscapeDataString(accountName)}:{Uri.EscapeDataString(key)}@" +
                        $"{accountName}.mongo.cosmos.azure.com/?retryWrites=false&tls=true";
                }
            }
            else
            {
                // Falls jemand ein nacktes mongo:// ohne retryWrites=false setzt, ergänzen
                if (!conn.Contains("retryWrites", StringComparison.OrdinalIgnoreCase))
                {
                    conn += (conn.Contains("?") ? "&" : "?") + "retryWrites=false";
                }
            }

            var client = new MongoClient(conn);
            var database = client.GetDatabase(databaseName);
            _packages = database.GetCollection<Package>(collectionName);

            // Optional: Index für QrToken
            var idx = new CreateIndexModel<Package>(
                Builders<Package>.IndexKeys.Ascending(p => p.QrToken),
                new CreateIndexOptions { Unique = true, Sparse = true });
            _packages.Indexes.CreateOne(idx);
        }

        public string DatabaseName => _packages.Database.DatabaseNamespace.DatabaseName;
        public string CollectionName => _packages.CollectionNamespace.CollectionName;

        public Task<long> CountAsync() =>
            _packages.CountDocumentsAsync(Builders<Package>.Filter.Empty);

        public async Task<string?> FirstIdAsync()
        {
            var doc = await _packages.Find(Builders<Package>.Filter.Empty)
                                     .Project(Builders<Package>.Projection.Include("_id"))
                                     .FirstOrDefaultAsync();
            return doc?.GetValue("_id", default(BsonValue))?.ToString();
        }

        public Task<List<Package>> GetAllAsync() =>
            _packages.Find(Builders<Package>.Filter.Empty).ToListAsync();

        public Task<Package?> GetByQrTokenAsync(string token) =>
            _packages.Find(p => p.QrToken == token).FirstOrDefaultAsync();

        public Task CreateAsync(Package package) =>
            _packages.InsertOneAsync(package);

        public async Task<bool> UpdateStatusAsync(string id, string status, bool viaQr = false)
        {
            var filter = Builders<Package>.Filter.Eq(p => p.Id, id);
            var updates = new List<UpdateDefinition<Package>>
            {
                Builders<Package>.Update.Set(p => p.Status, status)
            };
            if (viaQr)
            {
                updates.Add(Builders<Package>.Update.Set(p => p.QrUsedAt, DateTime.UtcNow));
            }
            var update = Builders<Package>.Update.Combine(updates);

            var res = await _packages.UpdateOneAsync(filter, update);
            return res.ModifiedCount == 1;
        }

        public async Task<int> DeleteCollectedAsync(ICollection<string> ids)
        {
            if (ids.Count == 0) return 0;

            // support both string ids and ObjectId-compatible ids
            var objIds = ids
                .Select(x => ObjectId.TryParse(x, out var oid) ? oid : (ObjectId?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToArray();

            var filter = Builders<Package>.Filter.And(
                Builders<Package>.Filter.In(p => p.Id, ids),
                Builders<Package>.Filter.Eq(p => p.Status, "Collected")
            );

            // Also try matching raw ObjectId in case Id field stores it as _id
            if (objIds.Length > 0)
            {
                filter = Builders<Package>.Filter.Or(
                    filter,
                    Builders<Package>.Filter.And(
                        Builders<Package>.Filter.In("_id", objIds),
                        Builders<Package>.Filter.Eq(p => p.Status, "Collected")
                    )
                );
            }

            var res = await _packages.DeleteManyAsync(filter);
            return (int)res.DeletedCount;
        }
    }
}