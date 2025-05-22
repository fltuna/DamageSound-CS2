namespace DamageSound.Models;

public class SoundData(string modelName, string modelPath, string damageSoundName, string deathSoundName)
{
    /// <summary>
    /// Model name, it corresponds with toml section name <br/>
    /// e.g. when section name is "[ExampleName]" then model name is "ExampleName"
    /// </summary>
    public readonly string ModelName = modelName;
    
    /// <summary>
    /// Model path, it should be ends with .vmdl extension
    /// </summary>
    public readonly string ModelPath = modelPath;

    /// <summary>
    /// Damage sound name, this value used to CBaseEntity.EmitSound()
    /// </summary>
    public readonly string DamageSoundName = damageSoundName;

    /// <summary>
    /// Sound name, this value used to CBaseEntity.EmitSound()
    /// </summary>
    public readonly string DeathSoundName = deathSoundName;
}