using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RankUtils;

public class CacheRank
{
    private readonly string _cacheFilePath;
    private RankUtils _plugin;

    public CacheRank(RankUtils plugin)
    {
        _cacheFilePath = plugin.ModuleDirectory + "/cache_rank.json";
        _plugin = plugin;
        EnsureCacheFileExists();
    }

    private void EnsureCacheFileExists()
    {
        Utils.Log($"Cache file path: {_cacheFilePath}", Utils.TypeLog.DEBUG);
        if (File.Exists(_cacheFilePath)) return;
        Utils.Log("Cache file not found, creating new one...", Utils.TypeLog.DEBUG);
        File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(new List<CacheModel>()));
    }

    public CacheModel? GetCacheModel(string steamId)
    {
        var cacheModels = LoadCache();
        Utils.Log($"Cache loaded: {cacheModels.Count} records", Utils.TypeLog.DEBUG);
        return cacheModels.Find(x =>
            x.Steam == steamId && DateTime.UtcNow - x.Timestamp < TimeSpan.FromDays(_plugin.Config.CacheSaveBanRank));
    }

    private List<CacheModel> LoadCache()
    {
        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            return JsonSerializer.Deserialize<List<CacheModel>>(json) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public void AddToCache(CacheModel model)
    {
        if (model.Steam == string.Empty || _plugin.Config.CacheSaveBanRank == 0) return;
        var cache = LoadCache();

        cache.RemoveAll(x =>
            DateTime.UtcNow - x.Timestamp > TimeSpan.FromDays(_plugin.Config.CacheSaveBanRank) ||
            x.Steam == model.Steam);

        _plugin.Logger.LogInformation("Add to cache: Nane:{0}, Steam:{1}, Value: {2}, Rank: {3}", model.Name,
            model.Steam, model.Value, model.Rank);

        cache.Add(model);

        SaveCache(cache);
    }

    public void RemoveFromCache(string steamId)
    {
        var cache = LoadCache();

        var cacheModel = cache.Find(x => x.Steam == steamId);

        if (cacheModel != null)
        {
            _plugin.Logger.LogInformation("Remove from cache: Nane:{0}, Steam:{1}, Value: {2}, Rank: {3}",
                cacheModel.Name, cacheModel.Steam, cacheModel.Value, cacheModel.Rank);
        }

        cache.RemoveAll(x => x.Steam == steamId);
        SaveCache(cache);
    }

    private void SaveCache(List<CacheModel> cache)
    {
        try
        {
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving cache: {ex.Message}");
        }
    }

    public void SaveTop10(List<CacheModel> cache, string type)
    {
        if (!_plugin.Config.SaveTop10)
        {
            Utils.Log("SaveTop10 disabled in config!", Utils.TypeLog.INFO);
            return;
        }

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm");
            var fileName = $"{type}_{timestamp}.json";
            var filePath = Path.Combine(_plugin.ModuleDirectory, fileName);
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            Utils.Log($"[SaveTop10] File saved: {filePath}", Utils.TypeLog.SUCCESS);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving cache: {ex.Message}");
        }
    }

    public class CacheModel
    {
        public string Steam { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; } = 0;
        public int Rank { get; set; } = 0;
        public int Kills { get; set; } = 0;
        public int Deaths { get; set; } = 0;
        public int Shoots { get; set; } = 0;
        public int Hits { get; set; } = 0;
        public int Headshots { get; set; } = 0;
        public int Assists { get; set; } = 0;
        public int Round_Win { get; set; } = 0;
        public int Round_Lose { get; set; } = 0;
        public int Playtime { get; set; } = 0;
        public int Lastconnect { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow; // Время добавления в кэш
    }
}