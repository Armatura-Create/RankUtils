using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RankUtils;

public class CacheRank
{
    private readonly string _cacheFilePath;
    private readonly PluginConfig _pluginConfig;
    
    public CacheRank(PluginConfig pluginConfig, string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        _pluginConfig = pluginConfig;
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
        return cacheModels.Find(x => x.Steam == steamId &&  DateTime.UtcNow - x.Timestamp < TimeSpan.FromDays(_pluginConfig.CacheSaveBanRank));
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
        if (model.Steam == string.Empty) return;
        var cache = LoadCache();

        cache.RemoveAll(x => DateTime.UtcNow - x.Timestamp > TimeSpan.FromDays(_pluginConfig.CacheSaveBanRank) || x.Steam == model.Steam);
        
        cache.Add(model);

        SaveCache(cache);
    }
    
    public void RemoveFromCache(string steamId)
    {
        var cache = LoadCache();
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