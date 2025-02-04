using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using IksAdminApi;
using Microsoft.Extensions.Logging;
using RanksApi;

namespace ClearRanksWithBan;

[MinimumApiVersion(305)]
public class RankUtils : AdminModule
{
    public override string ModuleName => "RankUtils";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "Armatura";

    private IRanksApi? _api;
    private DbService? _dbService;

    private bool _isReady;

    public override void Ready()
    {
        _api = IRanksApi.Capability.Get();
        if (_api == null)
        {
            Logger.LogError("[Ranks] RanksApi не установлен или не доступен.");
            return;
        }

        _dbService = new DbService(this, _api, Api);

        _isReady = true;
        
        _dbService?.EnsurePrimaryKeyExists();

        Utils.Log("Ready", Utils.TypeLog.SUCCESS);
        Api.OnBanPost += OnBanPlayerPost;
    }

    private HookResult OnBanPlayerPost(PlayerBan ban, ref bool announce)
    {
        if (_api == null || string.IsNullOrEmpty(ban.SteamId)) return HookResult.Continue;

        AdminUtils.LogDebug("[ClearRanksWithBan] BAN POST FROM MODULE:");
        AdminUtils.LogDebug($"[ClearRanksWithBan] Admin name: {ban.Admin}");
        AdminUtils.LogDebug($"[ClearRanksWithBan] Player name: {ban.Name}");
        AdminUtils.LogDebug($"[ClearRanksWithBan] Reason: {ban.Reason}");
        AdminUtils.LogDebug("[ClearRanksWithBan] Try Clearing rank exp...");

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
        
        var arg = info.GetArg(0);
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
}