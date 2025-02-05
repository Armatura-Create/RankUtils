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

    public Task StartAsync()
    {
        if (_pluginConfig.CronSettings.Count == 0)
        {
            Utils.Log("[CronService] No CRON jobs configured.", Utils.TypeLog.WARN);
            return Task.CompletedTask;
        }

        foreach (var cronSetting in _pluginConfig.CronSettings)
        {
            _ = Task.Run(() => RunCronJob(cronSetting), _cts.Token);
        }

        return Task.CompletedTask;
    }

    private async Task RunCronJob(PluginConfig.Cron cronSetting)
    {
        DateTime? nextRun = CronExpression.Parse(cronSetting.CronExpression).GetNextOccurrence(DateTime.UtcNow);
        while (!_cts.Token.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            if (nextRun == null)
            {
                Utils.Log($"[CronService] No next execution time for command: {cronSetting.Command}", Utils.TypeLog.DEBUG);
                break;
            }

            var delay = nextRun.Value - now;
            if (delay <= TimeSpan.Zero)
            {
                Utils.Log($"[CronService] Skipping invalid execution time for command: {cronSetting.Command}", Utils.TypeLog.DEBUG);
                continue;
            }

            Utils.Log($"[CronService] Next cron execution in: {Utils.FormatDelay(delay)}", Utils.TypeLog.INFO);
            Utils.Log($"[CronService] Scheduled command: {cronSetting.Command}", Utils.TypeLog.INFO);

            try
            {
                await Task.Delay(delay, _cts.Token);
                ExecuteCommand(cronSetting.Command);
            }
            catch (TaskCanceledException)
            {
                Utils.Log("[CronService] Task cancelled.", Utils.TypeLog.INFO);
                break;
            }
            catch (Exception ex)
            {
                Utils.Log($"[CronService] Error in execution loop: {ex.Message}", Utils.TypeLog.WARN);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token);
            
            nextRun = CronExpression.Parse(cronSetting.CronExpression).GetNextOccurrence(DateTime.UtcNow);
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