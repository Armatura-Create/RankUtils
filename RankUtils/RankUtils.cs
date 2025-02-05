using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using RanksApi;

namespace RankUtils;

[MinimumApiVersion(305)]
public class RankUtils : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "RankUtils";
    public override string ModuleAuthor => "Armatura";
    public override string ModuleVersion => "1.0.1";

    private IRanksApi? _api;
    private DbService? _dbService;
    
    private readonly CronJobService _cronJobService = new();
    
    public PluginConfig Config { get; set; }
    public CacheRank CacheRank { get; set; }

    private bool _isReady;
    
    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        CacheRank = new CacheRank(config, ModuleDirectory + "/cache_rank.json");

        if (Config.CacheSaveBanRank > 7)
        {
            Config.CacheSaveBanRank = 7;
        }
        
        _cronJobService.InitializeConfig(Config);
        
        Utils.Log("[RankUtils] Config parsed!", Utils.TypeLog.DEBUG);
    }

    public override void Load(bool hotReload)
    {
        Utils.Log("[RankUtils] Plugin loaded!", Utils.TypeLog.SUCCESS);
        _ = _cronJobService.StartAsync();
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

        Utils.Log($"BAN EVENT: Admin: {ban.Admin}, Player: {ban.Name}, Reason: {ban.Reason}", Utils.TypeLog.DEBUG);
        
        try
        {
            _dbService?.SetBanExp(ban.SteamId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
        Utils.Log("Clearing rank exp for banned players...", Utils.TypeLog.INFO);
        if (_isReady) _dbService?.StartClearOldBan();
    }
    
    // css_lr_reset_ranks - сбрасывает статистику у всех игроков.
    // all - сбросит все данные.
    // exp - сбросит данные о очках опыта (value, rank).
    // stats - сбросит данные о статистике (kills, deaths, shoots, hits, headshots, assists, round_win, round_lose).
    [CommandHelper(minArgs: 1, usage: "css_lr_reset_ranks <all|exp|stats|play_time>", whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_lr_reset_ranks")]
    public void ResetRanks(CCSPlayerController? player, CommandInfo info)
    {
        if (info.ArgCount < 1) return;
        
        var arg = info.GetArg(1);
        switch (arg)
        {
            case "all":
                _dbService?.ResetAll();
                break;
            case "exp":
                _dbService?.ResetExp();
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