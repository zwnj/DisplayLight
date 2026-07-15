namespace DisplayLight.App.Infrastructure.Windows;

internal interface IPowerSchemeNativeApi
{
    Guid GetActiveScheme();

    uint ReadAcValue(Guid schemeGuid);

    uint ReadDcValue(Guid schemeGuid);

    void WriteAcValue(Guid schemeGuid, uint seconds);

    void WriteDcValue(Guid schemeGuid, uint seconds);

    void Activate(Guid schemeGuid);
}
