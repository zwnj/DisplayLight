using DisplayLight.Core.Power;

namespace DisplayLight.App.Infrastructure.Windows;

internal interface IDisplayOffRecoveryStore
{
    DisplayOffRecoveryState? Load();

    void Save(DisplayOffRecoveryState state);

    void Delete();
}

internal sealed record DisplayOffRecoveryState(
    Guid SchemeGuid,
    PowerSettingTarget Target,
    uint OriginalSeconds,
    uint TemporarySeconds,
    DateTimeOffset CreatedAt);
