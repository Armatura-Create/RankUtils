using System.Text.RegularExpressions;
using Dapper;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using RanksApi;

namespace ClearRanksWithBan;

public class DbService(RankUtils plugin, IRanksApi ranksApi, IIksAdminApi iksAdminApi)
{
    public async Task EnsurePrimaryKeyExists()
    {
        try
        {
            Utils.Log("Starting to ensure primary key exists", Utils.TypeLog.DEBUG);

            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            // 1. Проверка наличия PK
            const string checkPkQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = @DatabaseName
              AND TABLE_NAME = @TableName
              AND CONSTRAINT_TYPE = 'PRIMARY KEY'";

            var pkExists = await connection.ExecuteScalarAsync<int>(checkPkQuery, new
            {
                DatabaseName = Utils.GetDatabaseName(ranksApi.DatabaseConnectionString),
                TableName = ranksApi.DatabaseTableName
            });

            if (pkExists == 0)
            {
                Utils.Log("No primary key found. Starting cleanup.", Utils.TypeLog.DEBUG);

                // 2. Добавление временного id (если его нет)
                await connection.ExecuteAsync($@"
                    ALTER TABLE `{ranksApi.DatabaseTableName}`
                    ADD COLUMN id BIGINT AUTO_INCREMENT PRIMARY KEY;
                ");

                // 3. Удаление дубликатов
                var cleanUpDuplicatesQuery = $@"
                    DELETE t1
                    FROM `{ranksApi.DatabaseTableName}` t1
                    LEFT JOIN (
                        SELECT steam, MAX(lastconnect) AS max_lastconnect, MIN(id) AS min_id
                        FROM `{ranksApi.DatabaseTableName}`
                        GROUP BY steam
                    ) t2
                    ON t1.steam = t2.steam AND t1.lastconnect = t2.max_lastconnect AND t1.id = t2.min_id
                    WHERE t2.min_id IS NULL;
                ";

                await connection.ExecuteAsync(cleanUpDuplicatesQuery);
                Utils.Log("Duplicate records cleaned successfully.", Utils.TypeLog.DEBUG);

                // 4. Удаление временного id
                await connection.ExecuteAsync($@"
                    ALTER TABLE `{ranksApi.DatabaseTableName}`
                    DROP COLUMN id;
                ");

                // 5. Добавление первичного ключа для steam
                Utils.Log("Creating primary key...", Utils.TypeLog.DEBUG);
                var addPkQuery = $@"
                    ALTER TABLE `{ranksApi.DatabaseTableName}`
                    ADD PRIMARY KEY (`steam`);
                ";

                await connection.ExecuteAsync(addPkQuery);
                Utils.Log("[ClearRanksWithBan] Primary key added successfully.", Utils.TypeLog.DEBUG);
            }
            else
            {
                Utils.Log("[ClearRanksWithBan] Primary key already exists. SUCCESS", Utils.TypeLog.DEBUG);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
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

            Utils.Log($"[ClearRanksWithBan] Clearing for {steamId}", Utils.TypeLog.DEBUG);
            await connection.ExecuteAsync(updateQuery, new
            {
                SteamId = Utils.SteamId64ToSteamId(steamId),
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

            Utils.Log("Clearing old bans is done.", Utils.TypeLog.SUCCESS);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task ResetAll()
    {
        try
        {
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            var resetQuery = $@"
                UPDATE `{ranksApi.DatabaseTableName}`
                SET `value` = 0, `rank` = 0, `kills` = 0, `deaths` = 0, `shoots` = 0, `hits` = 0, `headshots` = 0, `assists` = 0, `round_win` = 0, `round_lose` = 0, `playtime` = 0";

            await connection.ExecuteAsync(resetQuery);
            Utils.Log("All data reset successfully.", Utils.TypeLog.SUCCESS);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task ResetExp()
    {
        try
        {
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            var resetQuery = $@"
                UPDATE `{ranksApi.DatabaseTableName}`
                SET `value` = 0, `rank` = 0";

            await connection.ExecuteAsync(resetQuery);
            Utils.Log("Experience data reset successfully.", Utils.TypeLog.SUCCESS);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task ResetStats()
    {
        try
        {
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            var resetQuery = $@"
                UPDATE `{ranksApi.DatabaseTableName}`
                SET `kills` = 0, `deaths` = 0, `shoots` = 0, `hits` = 0, `headshots` = 0, `assists` = 0, `round_win` = 0, `round_lose` = 0";

            await connection.ExecuteAsync(resetQuery);
            Utils.Log("Stats data reset successfully.", Utils.TypeLog.SUCCESS);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task ResetPlayTime()
    {
        try
        {
            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            var resetQuery = $@"
                UPDATE `{ranksApi.DatabaseTableName}`
                SET `playtime` = 0";

            await connection.ExecuteAsync(resetQuery);
            Utils.Log("Playtime data reset successfully.", Utils.TypeLog.SUCCESS);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}