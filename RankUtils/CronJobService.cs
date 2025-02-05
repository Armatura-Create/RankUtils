using CounterStrikeSharp.API;
using Cronos;

namespace RankUtils;

public class CronJobService
{
    private readonly CancellationTokenSource _cts = new();
    private PluginConfig _pluginConfig;

    public void InitializeConfig(PluginConfig pluginConfig)
    {
        _pluginConfig = pluginConfig;
    }

    public async Task StartAsync()
    {
        if (_pluginConfig.CronSettings.Count == 0)
        {
            Utils.Log("[CronService] No CRON jobs configured.", Utils.TypeLog.WARN);
            return;
        }

        foreach (var cronSetting in _pluginConfig.CronSettings)
        {
            _ = Task.Run(() => RunCronJob(cronSetting), _cts.Token);
        }
    }

    private async Task RunCronJob(PluginConfig.Cron cronSetting)
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            var nextRun = CronExpression.Parse(cronSetting.CronExpression).GetNextOccurrence(DateTime.UtcNow);
            if (nextRun == null) continue;

            var delay = nextRun.Value - DateTime.UtcNow;
            Utils.Log($"[CronService] Next cron execution in: {delay}", Utils.TypeLog.INFO);
            Utils.Log($"[CronService] Scheduled command: {cronSetting.Command}", Utils.TypeLog.INFO);

            await Task.Delay(delay, _cts.Token);
            ExecuteCommand(cronSetting.Command);
        }
    }

    private void ExecuteCommand(string command)
    {
        try
        {
            Utils.Log($"[CronService] Executing command: {command}", Utils.TypeLog.INFO);
            Server.NextWorldUpdate(() => Server.ExecuteCommand(command));
        }
        catch (Exception e)
        {
            Utils.Log($"[CronService] Error: {e.Message}", Utils.TypeLog.WARN);
        }
    }

    public void Stop()
    {
        _cts.Cancel();
    }
    
    public void Restart()
    {
        Stop();
        _ = StartAsync();
    }
}