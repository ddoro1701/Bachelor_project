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
        private readonly LecturerService _lecturers;

        public PackageService(IConfiguration configuration, LecturerService lecturers)
        {
            _lecturers = lecturers;
            var conn = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
                ?? configuration["COSMOS_CONNECTION_STRING"]
                ?? configuration["MongoDB:ConnectionString"]
                ?? throw new Exception("No DB connection string configured.");
            var dbName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME")
                ?? configuration["COSMOS_DATABASE_NAME"]
                ?? configuration["MongoDB:DatabaseName"]
                ?? throw new Exception("No DB name configured.");
            var collName = Environment.GetEnvironmentVariable("COSMOS_PACKAGE_COLLECTION_NAME")
                ?? configuration["COSMOS_PACKAGE_COLLECTION_NAME"]
                ?? configuration["MongoDB:PackageCollectionName"]
                ?? "Packages";

            var client = new MongoClient(conn);
            var database = client.GetDatabase(dbName);
            _packages = database.GetCollection<Package>(collName);
        }

        public async Task CreateAsync(Package pkg)
        {
            // Enrichment: Namen nachziehen, falls nicht vorhanden
            if (!string.IsNullOrWhiteSpace(pkg.LecturerEmail) &&
                (string.IsNullOrWhiteSpace(pkg.LecturerFirstName) || string.IsNullOrWhiteSpace(pkg.LecturerLastName)))
            {
                var lec = await _lecturers.GetByEmailAsync(pkg.LecturerEmail.Trim());
                if (lec != null)
                {
                    pkg.LecturerFirstName ??= lec.FirstName;
                    pkg.LecturerLastName  ??= lec.LastName;
                    if (string.IsNullOrWhiteSpace(pkg.LecturerFirstName) && !string.IsNullOrWhiteSpace(lec.Name))
                    {
                        var (first, last) = LecturerService.SplitFullName(lec.Name);
                        pkg.LecturerFirstName = first;
                        pkg.LecturerLastName = last;
                    }
                }
            }

            // Defaults/Normalize: Uhrzeit behalten und in UTC speichern
            if (pkg.CollectionDate.HasValue)
            {
                var dt = pkg.CollectionDate.Value;
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                pkg.CollectionDate = dt.ToUniversalTime();
            }
            else
            {
                pkg.CollectionDate = DateTime.UtcNow; // NICHT .Date, damit Uhrzeit erhalten bleibt
            }

            // QR-Token vor Insert erzeugen (14 Tage gültig)
            pkg.QrToken ??= Guid.NewGuid().ToString("N");
            pkg.QrExpiresAt ??= DateTime.UtcNow.AddDays(14);

            pkg.Status ??= "Received";
            await _packages.InsertOneAsync(pkg);
        }

        // Kompatibilität: ältere Aufrufer können weiter AddPackageAsync benutzen
        public Task AddPackageAsync(Package pkg) => CreateAsync(pkg);

        // Back-compat: bisheriger Name
        public async Task<List<Package>> GetAllPackagesAsync()
        {
            return await _packages.Find(_ => true).ToListAsync();
        }

        // Neuer, vom Controller verwendeter Name
        public async Task<List<Package>> GetAllAsync() // WebApplication1.Services.PackageService.GetAllAsync
        {
            return await _packages
                .Find(Builders<Package>.Filter.Empty)
                .SortByDescending(p => p.CollectionDate)
                .ToListAsync();
        }

        public async Task<bool> UpdatePackageStatusAsync(string id, string status, bool viaQr = false)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // 1) Status immer aktualisieren
            var filterById = Builders<Package>.Filter.Eq(p => p.Id, id);
            var statusUpdate = Builders<Package>.Update.Set(p => p.Status, status);
            var result = await _packages.UpdateOneAsync(filterById, statusUpdate);

            // 2) QrUsedAt nur setzen, wenn via QR und Zielstatus = Collected und noch nicht gesetzt
            if (viaQr && string.Equals(status, "Collected", StringComparison.OrdinalIgnoreCase))
            {
                var qrNotSet = Builders<Package>.Filter.Eq(p => p.QrUsedAt, (DateTime?)null);
                var setQrTime = Builders<Package>.Update.Set(p => p.QrUsedAt, DateTime.UtcNow);
                await _packages.UpdateOneAsync(Builders<Package>.Filter.And(filterById, qrNotSet), setQrTime);
            }

            return result.ModifiedCount > 0;
        }

        public async Task<long> DeleteCollectedAsync(IEnumerable<string> ids)
        {
            var idList = ids?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new();
            if (idList.Count == 0) return 0;
            var filter = Builders<Package>.Filter.In(p => p.Id, idList);
            var result = await _packages.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<Package?> GetByQrTokenAsync(string token)
            => await _packages.Find(p => p.QrToken == token).FirstOrDefaultAsync();

        public async Task<(bool updated, string? id, bool alreadyCollected)> AutoCollectByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return (false, null, false);
            var pkg = await _packages.Find(p => p.QrToken == token).FirstOrDefaultAsync();
            if (pkg == null) return (false, null, false);

            var already = string.Equals(pkg.Status, "Collected", StringComparison.OrdinalIgnoreCase);
            var ok = await UpdatePackageStatusAsync(pkg.Id!, "Collected", viaQr: true);
            return (ok || already, pkg.Id, already);
        }
    }
}