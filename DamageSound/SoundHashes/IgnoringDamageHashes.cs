using System.ComponentModel;

namespace DamageSound.SoundHashes;

public enum IgnoringDamageHashes: uint
{
    [Description("Player.Death")]
    Death = 46413566,
    [Description("Player.DamageFall")]
    DamageFall = 3926353328,
    [Description("Player.DamageFall.Fem")]
    DamageFallFem = 282152614,
    [Description("Player.DeathTaser")]
    DeathTaser = 3065316423,
    [Description("Player.DeathTaser_F")]
    DeathTaser_F = 2056150061,
}