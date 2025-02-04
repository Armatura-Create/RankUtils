namespace RankUtils;

public class CronJobService
{
    
    private string _cronExpression;
    private readonly CancellationTokenSource _cts = new();
    
    public CronJobService()
    {
        LoadConfig();
    }
    
    private void LoadConfig()
    {
        try
        {
            var configJson = File.ReadAllText("config.json");
            var config = JsonSerializer.Deserialize<ConfigModel>(configJson);
            _cronExpression = config?.CronExpression ?? "0 0 * * *"; // Если нет в JSON, то раз в сутки
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки конфига: {ex.Message}");
            _cronExpression = "0 0 * * *"; // Значение по умолчанию
        }
    }
    
    public async Task StartAsync()
    {
        Console.WriteLine($"[CronService] Запуск по CRON: {_cronExpression}");

        while (!_cts.Token.IsCancellationRequested)
        {
            var nextRun = CronExpression.Parse(_cronExpression).GetNextOccurrence(DateTime.UtcNow);
            if (nextRun == null) continue;

            var delay = nextRun.Value - DateTime.UtcNow;
            Console.WriteLine($"[CronService] Следующий запуск через: {delay}");

            await Task.Delay(delay, _cts.Token);
            ExecuteCommand();
        }
    }
}