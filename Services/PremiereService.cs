using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class PremiereService(
    IMongoDatabase database,
    IMemoryCache memoryCache,
    ILogger<PremiereService> logger) : IPremiereService
{
    private const string CacheKey = "upcoming-premieres";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IMongoCollection<PremiereDocument> _collection =
        database.GetCollection<PremiereDocument>("premieres");

    public async Task<List<PremiereDocument>> GetUpcomingPremieresAsync()
    {
        if (memoryCache.TryGetValue(CacheKey, out List<PremiereDocument>? cached) && cached is not null)
        {
            return cached;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var all = await _collection.Find(_ => true).ToListAsync();

        var premieres = all
            .Where(p => p.PremiereDateOnly >= today)
            .OrderBy(p => p.PremiereDateOnly)
            .ToList();

        memoryCache.Set(CacheKey, premieres, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1
        });

        logger.LogInformation("Loaded {Count} upcoming premieres from database", premieres.Count);

        return premieres;
    }
}
