using System.Reflection;
using DisplayLight.App.Presentation;
using Velopack;
using Velopack.Sources;

namespace DisplayLight.App.Infrastructure.Updates;

internal sealed class VelopackApplicationUpdateService : IApplicationUpdateService
{
    private const string RepositoryUrl = "https://github.com/zwnj/DisplayLight";

    private readonly UpdateManager manager = new(new GithubSource(RepositoryUrl, null, false));
    private UpdateInfo? pendingUpdate;

    public string CurrentVersionText
    {
        get
        {
            string? version = manager.CurrentVersion?.ToString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                return $"v{version}";
            }

            Assembly? assembly = Assembly.GetEntryAssembly();
            string? informational = assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                return $"v{informational.Split('+', StringSplitOptions.RemoveEmptyEntries)[0]}";
            }

            return "v?";
        }
    }

    public async Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!manager.IsInstalled)
        {
            pendingUpdate = null;
            return new ApplicationUpdateCheckResult(false, false, null);
        }

        pendingUpdate = await manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        return new ApplicationUpdateCheckResult(
            true,
            pendingUpdate is not null,
            pendingUpdate?.TargetFullRelease.Version.ToString());
    }

    public async Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken = default)
    {
        UpdateInfo update = pendingUpdate ?? throw new InvalidOperationException("適用する更新がありません。");
        cancellationToken.ThrowIfCancellationRequested();
        await manager.DownloadUpdatesAsync(update, progress.Report, cancellationToken);
    }

    public void ApplyAndRestart()
    {
        UpdateInfo update = pendingUpdate ?? throw new InvalidOperationException("適用する更新がありません。");
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}
