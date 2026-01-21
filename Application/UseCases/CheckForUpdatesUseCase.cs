using System;
using System.Threading;
using System.Threading.Tasks;
using Timer.Application.Interfaces;
using Timer.Application.Models;

namespace Timer.Application.UseCases;

public sealed class CheckForUpdatesUseCase
{
    private readonly IUpdateService _updateService;

    public CheckForUpdatesUseCase(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    public async Task<UpdateInfo?> ExecuteAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        var latest = await _updateService.GetLatestAsync(cancellationToken);
        if (latest is null || latest.Version <= currentVersion)
        {
            return null;
        }

        return latest;
    }
}
