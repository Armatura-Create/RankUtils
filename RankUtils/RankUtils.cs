using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using Cronos;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using RanksApi;

namespace RankUtils;

[MinimumApiVersion(305)]
public class RankUtils : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "RankUtils";
    public override string ModuleAuthor => "Armatura";
    public override string ModuleVersion => "1.0.5";

    public static bool IsDebug { get; set; }

    private IRanksApi? _api;
    private DbService? _dbService;

    private CronJobService _cronJobService;

    public PluginConfig Config { get; set; }
    private CacheRank CacheRank { get; set; }

    private bool _isReady;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        CacheRank = new CacheRank(this);

        if (Config.CacheSaveBanRank > 30)
        {
            Config.CacheSaveBanRank = 30;
        }

        IsDebug = Config.Debug;

        _cronJobService = new CronJobService(Config);

        Utils.Log("[RankUtils] Config parsed!", Utils.TypeLog.DEBUG);
    }

    public override void Load(bool hotReload)
    {
        Utils.Log("[RankUtils] Plugin loaded!", Utils.TypeLog.SUCCESS);
        _ = _cronJobService.StartAsync();
        if (!hotReload) return;
        _cronJobService.InitializeConfig(Config);
    }

    public override void Unload(bool hotReload)
    {
        Utils.Log("[RankUtils] Plugin unloaded!", Utils.TypeLog.INFO);
        _cronJobService.Stop();
        if (_api == null) return;
        Api.OnBanPost -= OnBanPlayerPost;
        Api.OnUnBanPost -= OnUnBanPlayerPost;
    }

    public override void Ready()
    {
        _api = IRanksApi.Capability.Get();
        if (_api == null)
        {
            Logger.LogError("[Ranks] RanksApi not installed or not available.");
            return;
        }

        _dbService = new DbService(_api, Api, CacheRank);

        _isReady = true;

        _dbService?.EnsurePrimaryKeyExists();

        Utils.Log("Ready", Utils.TypeLog.SUCCESS);
        Api.OnBanPost += OnBanPlayerPost;
        Api.OnUnBanPost += OnUnBanPlayerPost;
    }

    private HookResult OnBanPlayerPost(PlayerBan ban, ref bool announce)
    {
        if (_api == null || string.IsNullOrEmpty(ban.SteamId)) return HookResult.Continue;

        Utils.Log($"BAN EVENT: Admin: {ban.Admin?.Name}, Player: {ban.Name}, Reason: {ban.Reason}, Duration: {ban.Duration}",
            Utils.TypeLog.DEBUG);
        
        Utils.Log($"Duration save to cache {Config.CacheSaveBanRank * 24 * 60 * 60}", Utils.TypeLog.DEBUG);

        if (Config.CacheSaveBanRank > 0 && ban.Duration > Config.CacheSaveBanRank * 24 * 60 * 60)
        {
            Server.NextWorldUpdate(() =>
            {
                try
                {
                    var isOnline = false;
            
                    foreach (var player in Utilities.GetPlayers()
                                 .Where(player => player is { IsValid: true, IsBot: false, IsHLTV: false}))
                    {
                        Utils.Log("Player online: " + player.AuthorizedSteamID?.SteamId64, Utils.TypeLog.DEBUG);
                        if (player.AuthorizedSteamID?.SteamId64.ToString() != ban.SteamId) continue;
                        Utils.Log($"Player {player.AuthorizedSteamID.SteamId64} online yet - reset by api", Utils.TypeLog.DEBUG);
                        _api.SetPlayerExperience(player, 0);
                        isOnline = true;
                        break;
                    }
            
                    if (!isOnline)
                    {
                        _dbService?.SetBanExp(ban.SteamId);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }
        
        return HookResult.Continue;
    }

    private HookResult OnUnBanPlayerPost(Admin admin, ref string arg, ref string? reason, ref bool announce)
    {
        if (_api == null || string.IsNullOrEmpty(arg)) return HookResult.Continue;

        Utils.Log($"UNBAN EVENT: Admin: {admin.Name}, SteamId: {arg}", Utils.TypeLog.DEBUG);

        try
        {
            _dbService?.SetUnBanExp(arg);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return HookResult.Continue;
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_lr_clear_rank_if_banned")]
    public void ClearRankIfBanned(CCSPlayerController? player, CommandInfo info)
    {
        if (!_isReady)
        {
            Utils.Log("Plugin Ranks API is not ready yet.", Utils.TypeLog.WARN);
        }

        Utils.Log("Clearing rank exp for banned players...", Utils.TypeLog.INFO);
        if (_isReady) _dbService?.StartClearOldBan();
    }

    // css_lr_reset_ranks - сбрасывает статистику у всех игроков.
    // all - сбросит все данные.
    // exp - сбросит данные о очках опыта (value, rank).
    // stats - сбросит данные о статистике (kills, deaths, shoots, hits, headshots, assists, round_win, round_lose).
    [CommandHelper(minArgs: 1, usage: "css_lr_reset_ranks <all|exp|stats|play_time>",
        whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_lr_reset_ranks")]
    public void ResetRanks(CCSPlayerController? player, CommandInfo info)
    {
        if (info.ArgCount < 1) return;

        if (!_isReady)
        {
            Utils.Log("Plugin Ranks API is not ready yet.", Utils.TypeLog.WARN);
            return;
        }

        var arg = info.GetArg(1);
        switch (arg)
        {
            case "all":
                _dbService?.ResetAll();
                break;
            case "exp":
                Server.NextWorldUpdate(() =>
                {
                    List<string> steamIds = [];
                    foreach (var p in Utilities.GetPlayers()
                                 .Where(p => player is { IsValid: true, IsBot: false, IsHLTV: false}))
                    {
                        if (p.AuthorizedSteamID == null) continue;
                        steamIds.Add(p.AuthorizedSteamID.SteamId64.ToString());
                    }
                    _dbService?.ResetExp(excludeSteamIds: steamIds);
                });
                break;
            case "stats":
                _dbService?.ResetStats();
                break;
            case "play_time":
                _dbService?.ResetPlayTime();
                break;
            default:
                Utils.Log("Invalid argument. Usage: css_lr_reset_ranks <all|exp|stats>", Utils.TypeLog.WARN);
                break;
        }
    }
    
    [CommandHelper(minArgs: 1, usage: "css_lr_reset_old_exp <NUMBER>",
        whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_lr_reset_old_exp")]
    public void ResetOldExp(CCSPlayerController? player, CommandInfo info)
    {
        if (info.ArgCount < 1) return;

        if (!_isReady)
        {
            Utils.Log("Plugin Ranks API is not ready yet.", Utils.TypeLog.WARN);
            return;
        }
        
        if (!int.TryParse(info.GetArg(1), out var arg))
        {
            Utils.Log("Invalid argument. Please provide a valid number of days.", Utils.TypeLog.WARN);
            return;
        }

        for (int i = 0; i < info.ArgCount; i++)
        {
            Utils.Log($"{info.GetArg(i)}", Utils.TypeLog.DEBUG);
        }

        try
        {
            _dbService?.ResetExp(arg);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_ru_cron_list")]
    public void CronList(CCSPlayerController? player, CommandInfo info)
    {
        Utils.Log("Cron list:", Utils.TypeLog.INFO);

        if (Config.CronSettings.Count == 0)
        {
            Utils.Log("No CRON jobs configured.", Utils.TypeLog.WARN);
            return;
        }

        foreach (var cronSetting in Config.CronSettings)
        {
            try
            {
                // Вычисляем следующее время выполнения
                var nextRun = CronExpression.Parse(cronSetting.CronExpression)
                    .GetNextOccurrence(DateTime.UtcNow);

                if (nextRun.HasValue)
                {
                    var formattedNextRun = nextRun.Value.ToString("yyyy-MM-dd HH:mm");
                    Utils.Log($"Command: {cronSetting.Command} | Next run: {formattedNextRun}", Utils.TypeLog.INFO);
                }
                else
                {
                    Utils.Log($"Command: {cronSetting.Command} | No next execution time found.", Utils.TypeLog.WARN);
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"Error parsing CRON expression for command {cronSetting.Command}: {ex.Message}",
                    Utils.TypeLog.WARN);
            }
        }
    }

    // [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    // [ConsoleCommand("css_ru_reload_config")]
    // public void ReloadConfig(CCSPlayerController? player, CommandInfo info)
    // {
    //     Utils.Log("[RankUtils] Reloading config...", Utils.TypeLog.INFO);
    //
    //     try
    //     {
    //         var newConfig = PluginConfigLoader.LoadConfig<PluginConfig>();
    //
    //         if (newConfig != null)
    //         {
    //             Config = newConfig;
    //             OnConfigParsed(Config);
    //             _cronJobService.Restart();
    //             Utils.Log("[RankUtils] Config reloaded successfully!", Utils.TypeLog.SUCCESS);
    //         }
    //         else
    //         {
    //             Utils.Log("[RankUtils] Failed to reload config.", Utils.TypeLog.WARN);
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         Utils.Log($"[RankUtils] Error reloading config: {e.Message}", Utils.TypeLog.WARN);
    //     }
    // }
}