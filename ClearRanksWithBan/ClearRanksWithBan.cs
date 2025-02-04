using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using IksAdminApi;
using RanksApi;

namespace ClearRanksWithBan;

[MinimumApiVersion(305)]
public class ClearRanksWithBan : AdminModule
{
    public override string ModuleName => "ClearRanksWithBan";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Armatura";

    private IRanksApi? _api;
    private DBService? _dbService;

    private bool isReady = false;

    public override void Ready()
    {
        _api = IRanksApi.Capability.Get();
        if (_api == null)
        {
            AdminUtils.LogError("[Ranks] RanksApi не установлен или не доступен.");
            return;
        }

        _dbService = new DBService(_api, Api);

        isReady = true;

        AdminUtils.LogDebug("[ClearRanksWithBan] Ready");
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
        AdminUtils.LogDebug("[ClearRanksWithBan] Clearing rank exp for banned players...");
        if (isReady) _dbService?.StartClearOldBan();
    }
}