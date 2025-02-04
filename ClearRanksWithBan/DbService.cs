using System.Text.RegularExpressions;
using Dapper;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using RanksApi;

namespace ClearRanksWithBan;

public class DbService(ClearRanksWithBan plugin, IRanksApi ranksApi, IIksAdminApi iksAdminApi)
{
    public async Task EnsurePrimaryKeyExists()
    {
        try
        {
            plugin.Logger.LogDebug("Starting to ensure primary key exists");
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            // SQL для проверки наличия PK
            const string checkPkQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = @DatabaseName
                  AND TABLE_NAME = @TableName
                  AND CONSTRAINT_TYPE = 'PRIMARY KEY'";

            var pkExists = await connection.ExecuteScalarAsync<int>(checkPkQuery, new
            {
                DatabaseName = GetDatabaseName(ranksApi.DatabaseConnectionString),
                TableName = ranksApi.DatabaseTableName
            });

            if (pkExists == 0)
            {
                plugin.Logger.LogDebug("[ClearRanksWithBan] Primary key does not exist. Creating...");
                var cleanUpDuplicatesQuery = $@"
                    -- 1. Создать временную таблицу с уникальными записями
                    CREATE TEMPORARY TABLE temp_table AS
                    SELECT *
                    FROM `{ranksApi.DatabaseTableName}` t1
                    WHERE t1.lastconnect = (
                        SELECT MAX(t2.lastconnect)
                        FROM `{ranksApi.DatabaseTableName}` t2
                        WHERE t2.steam = t1.steam
                    );

                    -- 2. Очистить оригинальную таблицу
                    TRUNCATE TABLE `{ranksApi.DatabaseTableName}`;

                    -- 3. Вставить уникальные записи обратно
                    INSERT INTO `{ranksApi.DatabaseTableName}`
                    SELECT *
                    FROM temp_table;

                    -- 4. Удалить временную таблицу
                    DROP TEMPORARY TABLE temp_table;";

                await connection.ExecuteAsync(cleanUpDuplicatesQuery);
                plugin.Logger.LogDebug("[ClearRanksWithBan] Duplicate records cleaned successfully.");

                var addPkQuery = $@"
                    ALTER TABLE `{ranksApi.DatabaseTableName}`
                    ADD PRIMARY KEY (`steam`)";
        
                await connection.ExecuteAsync(addPkQuery);
                plugin.Logger.LogDebug("[ClearRanksWithBan] Primary key added successfully.");
            }
            else
            {
                plugin.Logger.LogDebug("[ClearRanksWithBan] Primary key already exists. SUCCESS");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ensuring primary key: {ex.Message}");
        }
    }
    
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

            plugin.Logger.LogDebug($"[ClearRanksWithBan] Clearing for {steamId}");
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
    
    private string GetDatabaseName(string connectionString)
    {
        var match = Regex.Match(connectionString, @"Database\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : throw new ArgumentException("Database name not found in connection string");
    }
}