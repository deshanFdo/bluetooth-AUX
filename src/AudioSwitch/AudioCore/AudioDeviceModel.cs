namespace AudioSwitch.AudioCore;

internal enum AudioCategory
{
    AuxWired,
    Bluetooth,
    BuiltInSpeakers,
    BuiltInMic,
    Other
}

internal sealed class AudioEndpointInfo
{
    public required string Id { get; init; }
    public required EDataFlow DataFlow { get; init; }
    public required string FriendlyName { get; init; }
    public required string ShortName { get; init; }
    public required string AdapterName { get; init; }
    public required string EnumeratorName { get; init; }
    public required DeviceState State { get; init; }
    public required EndpointFormFactor FormFactor { get; init; }
    public required AudioCategory Category { get; init; }
    public bool IsHandsFree { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsDefaultPlayback { get; init; }
    public bool IsDefaultCommunications { get; init; }
    public int VolumePercent { get; init; }
    public bool IsMuted { get; init; }

    public bool IsActive => State == DeviceState.Active;
    public bool IsDisabled => State == DeviceState.Disabled;
    public bool IsUnplugged => State == DeviceState.Unplugged;

    public string StateLabel => State switch
    {
        DeviceState.Active when IsDefaultPlayback => "Active • Default",
        DeviceState.Active when IsMuted => "Connected • Muted",
        DeviceState.Active => "Connected • Standby",
        DeviceState.Disabled => "Disabled in Windows",
        DeviceState.Unplugged => Category == AudioCategory.Bluetooth ? "Paired • Disconnected" : "Unplugged",
        DeviceState.NotPresent => "Not Present",
        _ => State.ToString()
    };
}
