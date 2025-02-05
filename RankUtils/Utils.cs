using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core.Plugin;

namespace RankUtils;

public static class Utils
{
    public static void Log(string message, TypeLog type)
    {
        if (!RankUtils.IsDebug && type == TypeLog.DEBUG) return;
        
        Console.ForegroundColor = GetConsoleColor(type);
        Console.WriteLine($"[RankUtils] [{type}] {message}");
        Console.ResetColor();
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
    
    private static ConsoleColor GetConsoleColor(TypeLog type)
    {
        return type switch
        {
            TypeLog.INFO => ConsoleColor.Cyan,      // Голубой для информационных сообщений
            TypeLog.WARN => ConsoleColor.Yellow,    // Желтый для предупреждений
            TypeLog.SUCCESS => ConsoleColor.Green,  // Зеленый для успешных операций
            TypeLog.DEBUG => ConsoleColor.Blue,     // Серый для отладочной информации
            _ => ConsoleColor.White                 // Белый по умолчанию
        };
    }
    
    public static string FormatDelay(TimeSpan delay)
    {
        return $"{delay.Days}d {delay.Hours}h {delay.Minutes}m {delay.Seconds}s";
    }

    public enum TypeLog
    {
        INFO,
        WARN,
        SUCCESS,
        DEBUG,
    }
}