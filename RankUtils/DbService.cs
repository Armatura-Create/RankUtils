using Dapper;
using IksAdminApi;
using MySqlConnector;
using RanksApi;

namespace RankUtils;

public class DbService(IRanksApi ranksApi, IIksAdminApi iksAdminApi, CacheRank cacheRank)
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
                Utils.Log("[RankUtils] Primary key added successfully.", Utils.TypeLog.DEBUG);
            }
            else
            {
                Utils.Log("[RankUtils] Primary key already exists. SUCCESS", Utils.TypeLog.DEBUG);
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

            var selectQuery = $@"
                SELECT `steam`, `name`, `value`, `rank`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `playtime`, `lastconnect`
                FROM `{ranksApi.DatabaseTableName}`
                WHERE `steam` = @SteamId";

            var existingData = await connection.QueryFirstOrDefaultAsync<CacheRank.CacheModel>(selectQuery, new
            {
                SteamId = Utils.SteamId64ToSteamId(steamId)
            });

            if (existingData != null)
            {
                cacheRank.AddToCache(existingData);
                Utils.Log($"Cached data for SteamId: {existingData.Steam}", Utils.TypeLog.DEBUG);
            }
            else
            {
                Utils.Log($"No existing data found for SteamId: {steamId}", Utils.TypeLog.WARN);
            }

            var updateQuery = $@"
                INSERT INTO `{ranksApi.DatabaseTableName}` 
                    (`steam`, `name`, `value`, `rank`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `playtime`, `lastconnect`) 
                VALUES 
                    (@SteamId, 'Unknow', @Experience, @Level, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                ON DUPLICATE KEY UPDATE 
                    `value` = @Experience,
                    `rank` = @Level";

            Utils.Log($"[RankUtils] Clearing exp for {Utils.SteamId64ToSteamId(steamId)}", Utils.TypeLog.DEBUG);
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

    public async Task SetUnBanExp(string? steamId)
    {
        if (steamId == null)
        {
            throw new ArgumentException("SteamId is null");
        }

        try
        {
            // Проверяем наличие записи в кэше
            var cachedData = cacheRank.GetCacheModel(Utils.SteamId64ToSteamId(steamId));
            if (cachedData == null)
            {
                Utils.Log($"[RankUtils] No cached data found for SteamId: {Utils.SteamId64ToSteamId(steamId)}. Cannot restore data.", Utils.TypeLog.WARN);
                return;
            }

            await using var connection = new MySqlConnection(ranksApi.DatabaseConnectionString);
            await connection.OpenAsync();

            // Восстанавливаем данные в таблице из кэша
            var restoreQuery = $@"
                INSERT INTO `{ranksApi.DatabaseTableName}` 
                    (`steam`, `name`, `value`, `rank`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `playtime`, `lastconnect`) 
                VALUES 
                    (@SteamId, @Name, @Value, @Rank, @Kills, @Deaths, @Shoots, @Hits, @Headshots, @Assists, @RoundWin, @RoundLose, @Playtime, @Lastconnect)
                ON DUPLICATE KEY UPDATE 
                    `name` = @Name,
                    `value` = @Value,
                    `rank` = @Rank,
                    `kills` = @Kills,
                    `deaths` = @Deaths,
                    `shoots` = @Shoots,
                    `hits` = @Hits,
                    `headshots` = @Headshots,
                    `assists` = @Assists,
                    `round_win` = @RoundWin,
                    `round_lose` = @RoundLose,
                    `playtime` = @Playtime,
                    `lastconnect` = @Lastconnect";

            await connection.ExecuteAsync(restoreQuery, new
            {
                SteamId = cachedData.Steam,
                Name = cachedData.Name,
                Value = cachedData.Value,
                Rank = cachedData.Rank,
                Kills = cachedData.Kills,
                Deaths = cachedData.Deaths,
                Shoots = cachedData.Shoots,
                Hits = cachedData.Hits,
                Headshots = cachedData.Headshots,
                Assists = cachedData.Assists,
                RoundWin = cachedData.Round_Win,
                RoundLose = cachedData.Round_Lose,
                Playtime = cachedData.Playtime,
                Lastconnect = cachedData.Lastconnect
            });

            Utils.Log($"[RankUtils] Successfully restored data for SteamId: {steamId} from cache.", Utils.TypeLog.SUCCESS);
            
            cacheRank.RemoveFromCache(Utils.SteamId64ToSteamId(steamId));
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