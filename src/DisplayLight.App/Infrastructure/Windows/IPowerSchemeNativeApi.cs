namespace DisplayLight.App.Infrastructure.Windows;

internal interface IPowerSchemeNativeApi
{
    Guid GetActiveScheme();

    uint ReadAcDisplayValue(Guid schemeGuid);

    uint ReadDcDisplayValue(Guid schemeGuid);

    uint ReadAcSleepValue(Guid schemeGuid);

    uint ReadDcSleepValue(Guid schemeGuid);

    void WriteAcDisplayValue(Guid schemeGuid, uint seconds);

    void WriteDcDisplayValue(Guid schemeGuid, uint seconds);

    void WriteAcSleepValue(Guid schemeGuid, uint seconds);

    void WriteDcSleepValue(Guid schemeGuid, uint seconds);

    void Activate(Guid schemeGuid);
}
