using System.Runtime.InteropServices;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using DamageSound.SoundHashes;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace DamageSound;

public class DamageSound: BasePlugin
{
    public override string ModuleName => "DamageSound";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "tuna";
        
    public override void Load(bool hotReload)
    {
        HookUserMessage(208, HookSound, HookMode.Pre);
    }
    
    public override void Unload(bool hotReload)
    {
        UnhookUserMessage(208, HookSound);
    }

    private HookResult HookSound(UserMessage msg)
    {
        uint soundEventHash = msg.ReadUInt("soundevent_hash");

        if (Enum.IsDefined(typeof(IgnoringDamageHashes), soundEventHash))
        {
            msg.SetInt("soundevent_guid", 0);
            msg.SetInt("soundevent_hash", 0);
        }
        
        
        return HookResult.Continue;
    }
}
