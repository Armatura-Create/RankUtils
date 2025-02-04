using Dapper;
using IksAdminApi;
using MySqlConnector;
using RanksApi;

namespace ClearRanksWithBan;

public class DBService(IRanksApi ranksApi, IIksAdminApi iksAdminApi)
{
    public async Task SetBanExp(string? steamId)
    {
        if (steamId == null)
        {
            throw new ArgumentException("SteamId is null");
        }

        try
        {
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            var updateQuery = $@"
                INSERT INTO `{ranksApi.DatabaseTableName}` 
                    (`steam`, `name`, `value`, `rank`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `playtime`, `lastconnect`) 
                VALUES 
                    (@SteamId, 'Unknow', @Experience, @Level, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                ON DUPLICATE KEY UPDATE 
                    `value` = @Experience,
                    `rank` = @Level";

            AdminUtils.LogDebug($"[ClearRanksWithBan] Clearing for {steamId}");
            await connection.ExecuteAsync(updateQuery, new
            {
                SteamId = SteamId64ToSteamId(steamId),
                Level = 0,
                Experience = 0
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task StartClearOldBan()
    {
        try
        {
            await using var connection = new MySqlConnection(iksAdminApi.DbConnectionString);
            await connection.OpenAsync();

            var selectQuery = @"
                SELECT DISTINCT `steam_id`
                FROM `iks_bans`";

            var steamIds = await connection.QueryAsync<string>(selectQuery);
            foreach (var steamId in steamIds)
            {
                await SetBanExp(steamId);
            }
            AdminUtils.LogDebug("[ClearRanksWithBan] Clearing old bans is done.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    private string SteamId64ToSteamId(string steamId64)
    {
        if (!ulong.TryParse(steamId64, out var steamId))
            throw new ArgumentException($"Invalid SteamID64 - {steamId64}");

        var z = (steamId - 76561197960265728) / 2;
        var y = steamId % 2;

        return $"STEAM_1:{y}:{z}";
    }
}