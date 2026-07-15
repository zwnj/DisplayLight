using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DisplayLight.App.Infrastructure.Windows;

internal sealed class PowerSchemeNativeApi : IPowerSchemeNativeApi
{
    private static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoPowerDownTimeout = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

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

    public uint ReadAcValue(Guid schemeGuid)
    {
        uint result = PowerSchemeNativeMethods.PowerReadAcValueIndex(
            nint.Zero,
            in schemeGuid,
            in VideoSubgroup,
            in VideoPowerDownTimeout,
            out uint value);
        ThrowIfFailed(result, "AC電源時のディスプレイ消灯時間を取得できませんでした。");
        return value;
    }

    public uint ReadDcValue(Guid schemeGuid)
    {
        uint result = PowerSchemeNativeMethods.PowerReadDcValueIndex(
            nint.Zero,
            in schemeGuid,
            in VideoSubgroup,
            in VideoPowerDownTimeout,
            out uint value);
        ThrowIfFailed(result, "バッテリー時のディスプレイ消灯時間を取得できませんでした。");
        return value;
    }

    public void WriteAcValue(Guid schemeGuid, uint seconds)
    {
        uint result = PowerSchemeNativeMethods.PowerWriteAcValueIndex(
            nint.Zero,
            in schemeGuid,
            in VideoSubgroup,
            in VideoPowerDownTimeout,
            seconds);
        ThrowIfFailed(result, "AC電源時のディスプレイ消灯時間を変更できませんでした。");
    }

    public void WriteDcValue(Guid schemeGuid, uint seconds)
    {
        uint result = PowerSchemeNativeMethods.PowerWriteDcValueIndex(
            nint.Zero,
            in schemeGuid,
            in VideoSubgroup,
            in VideoPowerDownTimeout,
            seconds);
        ThrowIfFailed(result, "バッテリー時のディスプレイ消灯時間を変更できませんでした。");
    }

    public void Activate(Guid schemeGuid)
    {
        uint result = PowerSchemeNativeMethods.PowerSetActiveScheme(nint.Zero, in schemeGuid);
        ThrowIfFailed(result, "変更した電源プランを反映できませんでした。");
    }

    private static void ThrowIfFailed(uint errorCode, string message)
    {
        if (errorCode != 0)
        {
            throw new Win32Exception(unchecked((int)errorCode), message);
        }
    }
}
