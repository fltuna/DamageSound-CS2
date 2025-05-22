using DamageSound.Models;

namespace DamageSound.Config;

public sealed class PluginConfig(Dictionary<string, SoundData> damageSounds, bool botsEmitSound, IReadOnlyList<string> toggleCommandNames, IReadOnlyList<string> volumeCommandNames, string soundFilePath)
{
    public readonly IReadOnlyDictionary<string, SoundData> DamageSounds = damageSounds;
    
    public bool BotsEmitSound { get; } = botsEmitSound;

    public string SoundFilePath { get; } = soundFilePath;

    public IReadOnlyList<string> ToggleCommandNames { get; } = toggleCommandNames;
    public IReadOnlyList<string> VolumeCommandNames { get; } = volumeCommandNames;
}