using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiveFuelMap.Infrastructure.Parsing;

public sealed class FuelPriceParsingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ParserOptions> options,
    ILogger<FuelPriceParsingWorker> logger) : BackgroundService
{
    private readonly ParserOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Парсер цін пального вимкнено.");
            return;
        }

        if (_options.RunOnStartup)
            await RunOnceAsync(stoppingToken);

        var interval = _options.IntervalSeconds > 0
            ? TimeSpan.FromSeconds(Math.Max(30, _options.IntervalSeconds))
            : TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        logger.LogInformation("Парсер цін пального запущено. Інтервал={Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var parserService = scope.ServiceProvider.GetRequiredService<IFuelParserService>();
        await parserService.RunOnceAsync(cancellationToken);
    }
}

public sealed class FuelParserService(
    IUnitOfWork unitOfWork,
    IFuelPriceImportService importService,
    IPriceSourceClientFactory factory,
    IFuelUpdatesNotifier notifier,
    IOptions<ParserOptions> options,
    ILogger<FuelParserService> logger) : IFuelParserService
{
    private readonly ParserOptions _options = options.Value;

    public async Task<IReadOnlyList<ParserRunDto>> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var completedRuns = new List<ParserRunDto>();
        await DeleteExpiredPricesAsync(cancellationToken);

        var sources = await unitOfWork.DataSources.Query().Where(x => x.Enabled).ToListAsync(cancellationToken);

        foreach (var source in sources)
        {
            var run = await RunSourceAsync(source, cancellationToken);
            completedRuns.Add(new ParserRunDto(
                run.Id,
                source.Id,
                source.Name,
                run.StartedAt,
                run.FinishedAt,
                run.Status.ToString(),
                run.RecordsFound,
                run.RecordsSaved,
                run.Error));
        }

        return completedRuns;
    }

    private async Task DeleteExpiredPricesAsync(CancellationToken cancellationToken)
    {
        if (_options.RetentionDays <= 0)
            return;

        var cutoff = DateTime.UtcNow.Date.AddDays(-_options.RetentionDays);
        var deleted = await unitOfWork.FuelPrices.Query()
            .Where(x => x.Date < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            logger.LogInformation("🧹 Видалено записи цін старші за період зберігання: Count={Count}; Cutoff={Cutoff:yyyy-MM-dd}", deleted, cutoff);

        if (false && deleted > 0)
            logger.LogInformation("Видалено старі записи цін: Count={Count}; Cutoff={Cutoff:yyyy-MM-dd}", deleted, cutoff);
    }

    private async Task<ParserRun> RunSourceAsync(DataSource source, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var run = new ParserRun { SourceId = source.Id, StartedAt = startedAt, Status = ParserRunStatus.Running };
        await unitOfWork.ParserRuns.AddAsync(run, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            logger.LogInformation("🚀 Старт парсингу джерела {SourceName} ({Url})", source.Name, source.Url);
            var records = await factory.Create(source).FetchAsync(source, cancellationToken);
            var result = await importService.ImportAsync(source.Id, records, cancellationToken);

            run.Status = ParserRunStatus.Succeeded;
            run.RecordsFound = result.RecordsFound;
            run.RecordsSaved = result.RecordsSaved;
            run.FinishedAt = DateTime.UtcNow;
            run.Duration = run.FinishedAt - run.StartedAt;
            source.LastSuccessAt = run.FinishedAt;
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (result.RecordsSaved > 0)
                await notifier.NotifyFuelDataUpdatedAsync(result.PriceChanges ?? [], cancellationToken);

            logger.LogInformation(
                "✅ Парсинг завершено для {SourceName}. Found={Found}; Saved={Saved}; Duration={Duration}",
                source.Name,
                result.RecordsFound,
                result.RecordsSaved,
                run.Duration);
        }
        catch (Exception ex)
        {
            run.Status = ParserRunStatus.Failed;
            run.Error = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
            run.Duration = run.FinishedAt - run.StartedAt;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "❌ Помилка парсингу джерела {SourceName} ({Url})", source.Name, source.Url);
        }

        return run;
    }
}
