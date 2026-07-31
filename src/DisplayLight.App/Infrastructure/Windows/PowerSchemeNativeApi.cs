using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class PowerSchemeNativeApi : IPowerSchemeNativeApi
{
    private static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoPowerDownTimeout = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    private static readonly Guid SleepSubgroup = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid SleepIdleTimeout = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");

    public Guid GetActiveScheme()
    {
        nint schemePointer = nint.Zero;

        try
        {
            uint result = PowerSchemeNativeMethods.PowerGetActiveScheme(nint.Zero, out schemePointer);
            ThrowIfFailed(result, "現在の電源プランを取得できませんでした。");
            return Marshal.PtrToStructure<Guid>(schemePointer);
        }
        finally
        {
            if (schemePointer != nint.Zero)
            {
                _ = PowerSchemeNativeMethods.LocalFree(schemePointer);
            }
        }
    }

    public uint ReadAcDisplayValue(Guid schemeGuid) =>
        ReadAcValue(
            schemeGuid,
            VideoSubgroup,
            VideoPowerDownTimeout,
            "AC電源時のディスプレイ消灯時間を取得できませんでした。");

    public uint ReadDcDisplayValue(Guid schemeGuid) =>
        ReadDcValue(
            schemeGuid,
            VideoSubgroup,
            VideoPowerDownTimeout,
            "バッテリー時のディスプレイ消灯時間を取得できませんでした。");

    public uint ReadAcSleepValue(Guid schemeGuid) =>
        ReadAcValue(
            schemeGuid,
            SleepSubgroup,
            SleepIdleTimeout,
            "AC電源時のシステムスリープ時間を取得できませんでした。");

    public uint ReadDcSleepValue(Guid schemeGuid) =>
        ReadDcValue(
            schemeGuid,
            SleepSubgroup,
            SleepIdleTimeout,
            "バッテリー時のシステムスリープ時間を取得できませんでした。");

    public void WriteAcDisplayValue(Guid schemeGuid, uint seconds) =>
        WriteAcValue(
            schemeGuid,
            VideoSubgroup,
            VideoPowerDownTimeout,
            seconds,
            "AC電源時のディスプレイ消灯時間を変更できませんでした。");

    public void WriteDcDisplayValue(Guid schemeGuid, uint seconds) =>
        WriteDcValue(
            schemeGuid,
            VideoSubgroup,
            VideoPowerDownTimeout,
            seconds,
            "バッテリー時のディスプレイ消灯時間を変更できませんでした。");

    public void WriteAcSleepValue(Guid schemeGuid, uint seconds) =>
        WriteAcValue(
            schemeGuid,
            SleepSubgroup,
            SleepIdleTimeout,
            seconds,
            "AC電源時のシステムスリープ時間を変更できませんでした。");

    public void WriteDcSleepValue(Guid schemeGuid, uint seconds) =>
        WriteDcValue(
            schemeGuid,
            SleepSubgroup,
            SleepIdleTimeout,
            seconds,
            "バッテリー時のシステムスリープ時間を変更できませんでした。");

    public void Activate(Guid schemeGuid)
    {
        uint result = PowerSchemeNativeMethods.PowerSetActiveScheme(nint.Zero, in schemeGuid);
        ThrowIfFailed(result, "変更した電源プランを反映できませんでした。");
    }

    private static uint ReadAcValue(
        Guid schemeGuid,
        Guid subgroupGuid,
        Guid settingGuid,
        string errorMessage)
    {
        uint result = PowerSchemeNativeMethods.PowerReadAcValueIndex(
            nint.Zero,
            in schemeGuid,
            in subgroupGuid,
            in settingGuid,
            out uint value);
        ThrowIfFailed(result, errorMessage);
        return value;
    }

    private static uint ReadDcValue(
        Guid schemeGuid,
        Guid subgroupGuid,
        Guid settingGuid,
        string errorMessage)
    {
        uint result = PowerSchemeNativeMethods.PowerReadDcValueIndex(
            nint.Zero,
            in schemeGuid,
            in subgroupGuid,
            in settingGuid,
            out uint value);
        ThrowIfFailed(result, errorMessage);
        return value;
    }

    private static void WriteAcValue(
        Guid schemeGuid,
        Guid subgroupGuid,
        Guid settingGuid,
        uint seconds,
        string errorMessage)
    {
        uint result = PowerSchemeNativeMethods.PowerWriteAcValueIndex(
            nint.Zero,
            in schemeGuid,
            in subgroupGuid,
            in settingGuid,
            seconds);
        ThrowIfFailed(result, errorMessage);
    }

    private static void WriteDcValue(
        Guid schemeGuid,
        Guid subgroupGuid,
        Guid settingGuid,
        uint seconds,
        string errorMessage)
    {
        uint result = PowerSchemeNativeMethods.PowerWriteDcValueIndex(
            nint.Zero,
            in schemeGuid,
            in subgroupGuid,
            in settingGuid,
            seconds);
        ThrowIfFailed(result, errorMessage);
    }

    private static void ThrowIfFailed(uint errorCode, string message)
    {
        if (errorCode != 0)
        {
            throw new Win32Exception(unchecked((int)errorCode), message);
        }
    }
}
