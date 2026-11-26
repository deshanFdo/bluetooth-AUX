using System.Runtime.InteropServices;

namespace AudioSwitch.AudioCore;

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClientComObject
{
}

[ComImport]
[Guid("294935CE-F637-4E7C-A41B-AB255460B862")]
internal class PolicyConfigVistaClientComObject
{
}

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);

    [PreserveSig]
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, ref PropVariant pv);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
}

[ComImport]
[Guid("6BE54BE8-A068-4875-A49D-0C2966473B11")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig10
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);

    [PreserveSig]
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, ref PropVariant pv);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
}

[ComImport]
[Guid("568B9108-44BF-40B4-9006-86AFE5B5A620")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigVista
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, out PropVariant pv);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, ref PropVariant pv);

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
}

/// <summary>
/// Wraps Windows Core Audio PolicyConfig COM interfaces across Win7/10/11 builds.
/// </summary>
internal sealed class PolicyConfigClient
{
    public string ProbeSupportedInterface()
    {
        object? client = null;
        try
        {
            client = new PolicyConfigClientComObject();
            var supported = new List<string>();
            if (client is IPolicyConfig10) supported.Add("IPolicyConfig10");
            if (client is IPolicyConfig) supported.Add("IPolicyConfig");
            if (supported.Count > 0) return string.Join(", ", supported);
        }
        catch
        {
            // Fall through
        }
        finally
        {
            if (client != null && Marshal.IsComObject(client))
            {
                Marshal.ReleaseComObject(client);
            }
        }

        object? vista = null;
        try
        {
            vista = new PolicyConfigVistaClientComObject();
            if (vista is IPolicyConfigVista) return "IPolicyConfigVista";
        }
        catch
        {
            // Ignore
        }
        finally
        {
            if (vista != null && Marshal.IsComObject(vista))
            {
                Marshal.ReleaseComObject(vista);
            }
        }

        return "None";
    }

    public int SetDefaultEndpoint(string deviceId)
    {
        int hr = TryWithConfig(
            p10 => SetAllRoles(p10.SetDefaultEndpoint, deviceId),
            p7 => SetAllRoles(p7.SetDefaultEndpoint, deviceId),
            pv => SetAllRoles(pv.SetDefaultEndpoint, deviceId));
        return hr;
    }

    public int SetEndpointVisibility(string deviceId, bool visible)
    {
        return TryWithConfig(
            p10 => p10.SetEndpointVisibility(deviceId, visible),
            p7 => p7.SetEndpointVisibility(deviceId, visible),
            pv => pv.SetEndpointVisibility(deviceId, visible));
    }

    private static int SetAllRoles(Func<string, ERole, int> setter, string deviceId)
    {
        int hr = setter(deviceId, ERole.Console);
        if (hr != 0) return hr;
        hr = setter(deviceId, ERole.Multimedia);
        if (hr != 0) return hr;
        return setter(deviceId, ERole.Communications);
    }

    private static int TryWithConfig(
        Func<IPolicyConfig10, int> fn10,
        Func<IPolicyConfig, int> fn7,
        Func<IPolicyConfigVista, int> fnVista)
    {
        object? client = null;
        try
        {
            client = new PolicyConfigClientComObject();
            if (client is IPolicyConfig p7)
            {
                return fn7(p7);
            }
            if (client is IPolicyConfig10 p10)
            {
                return fn10(p10);
            }
        }
        catch (InvalidCastException)
        {
            // Fall through to Vista client
        }
        finally
        {
            if (client != null && Marshal.IsComObject(client))
            {
                Marshal.ReleaseComObject(client);
            }
        }

        object? vista = null;
        try
        {
            vista = new PolicyConfigVistaClientComObject();
            if (vista is IPolicyConfigVista pv)
            {
                return fnVista(pv);
            }
        }
        finally
        {
            if (vista != null && Marshal.IsComObject(vista))
            {
                Marshal.ReleaseComObject(vista);
            }
        }

        return unchecked((int)0x80004002); // E_NOINTERFACE
    }
}
