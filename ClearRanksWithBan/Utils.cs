using System.Text.RegularExpressions;
using Serilog;

namespace ClearRanksWithBan;

public static class Utils
{
    public static void Log(string message, TypeLog type)
    {
        Console.WriteLine("[RanksUtils]");
    }

    public static string SteamId64ToSteamId(string steamId64)
    {
        if (!ulong.TryParse(steamId64, out var steamId))
            throw new ArgumentException($"Invalid SteamID64 - {steamId64}");

        var z = (steamId - 76561197960265728) / 2;
        var y = steamId % 2;

        return $"STEAM_1:{y}:{z}";
    }

    public static string GetDatabaseName(string connectionString)
    {
        var match = Regex.Match(connectionString, @"Database\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : throw new ArgumentException("Database name not found in connection string");
    }

    public enum TypeLog
    {
        INFO,
        WARN,
        SUCCESS,
        DEBUG,
    }
}