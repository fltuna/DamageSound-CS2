using CounterStrikeSharp.API.Modules.Entities;

namespace DamageSound.Models;

public class DsPlayer
{
    public SteamID? SteamId { get; set; }

    private float _soundVolume = 1.0F;

    public float SoundVolume
    {
        get => _soundVolume;
        
        set
        {
            if (value > 1.0F)
            {
                value = 1.0F;
            }
            if (value < 0)
            {
                value = 0.0F;
            }
        
            _soundVolume = value;
        }
    }

    public bool IsSoundMuted { get; set; } = false;

    public float NextDamageSoundAvailableTime { get; set; } = 0.0F;
    public float NextDeathSoundAvailableTime { get; set; } = 0.0F;
    
    public string DamageSound { get; set; } = string.Empty;
    public string DeathSound { get; set; } = string.Empty;
}