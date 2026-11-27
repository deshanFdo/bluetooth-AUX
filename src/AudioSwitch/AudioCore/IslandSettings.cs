using System.IO;
using System.Text.Json;

namespace AudioSwitch.AudioCore;

internal sealed class IslandSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSwitch");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public bool ShowMediaPlayer { get; set; } = true;
    public bool ShowLaptopBattery { get; set; } = true;
    public bool ShowBluetoothBatteries { get; set; } = true;
    public bool ShowSystemStats { get; set; } = true;
    public bool ShowSpeakersCard { get; set; } = true;
    public bool ShowMultiOutput { get; set; } = true;
    public bool ShowPillEqualizer { get; set; } = true;
    public bool ShowPillMediaTitle { get; set; } = true;
    public bool ShowPillBattery { get; set; } = true;
    public bool MuteInactiveDevices { get; set; } = true;
    public bool HardDisableAux { get; set; } = false;
    public bool ShowAuxJackMic { get; set; } = false;
    public bool HideSpotifyToTray { get; set; } = true;
    public bool UseYouTubeMusicDefault { get; set; } = false;

    public static IslandSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<IslandSettings>(json) ?? new IslandSettings();
            }
        }
        catch
        {
            // Fall back to defaults on corrupt settings file
        }
        return new IslandSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore read-only disk errors
        }
    }
}
