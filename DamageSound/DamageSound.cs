using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using DamageSound.Config;
using DamageSound.Models;
using DamageSound.SoundHashes;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace DamageSound;

public class DamageSound: BasePlugin
{
    public override string ModuleName => "DamageSound";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "tuna";


    public FakeConVar<float> DamageSoundPlaybackCooldown = new(
        "ds_sound_playback_cooldown", 
        "cooldown of damage sound playback", 5.0F,ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 99999999.0F));

    public FakeConVar<float> DeathSoundPlaybackCooldown = new(
        "ds_sound_playback_cooldown",
        "cooldown of damage sound playback", 5.0F,ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 99999999.0F));

    public FakeConVar<float> SoundSyncInterval = new(
        "ds_sound_sync_interval",
        "Interval of sync player's damage/death sound with player's model", 1.0F, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0.0F, 600.0F));
    
    private readonly Dictionary<int, DsPlayer> _dsPlayers = new();
    
    private PluginConfig _pluginConfig = null!;
    
    
    private Timer? _modelCheckTimer;
    
    public override void Load(bool hotReload)
    {
        _pluginConfig = new ConfigParser(Path.Combine(ModuleDirectory, "configs", "dsconfig.toml"), this).Load();
        
        HookUserMessage(208, HookSound, HookMode.Pre);
        
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);

        if (_pluginConfig.SoundFilePath != string.Empty)
        {
        
            RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
            {
                // Sound path should end with vsndevts, not vsndevts_c
                manifest.AddResource(_pluginConfig.SoundFilePath);
            });
        }

        
        if (hotReload)
        {
            foreach (CCSPlayerController player in Utilities.GetPlayers())
            {
                InitPlayerDsSettings(player.Slot);
            }
        }

        SoundSyncInterval.ValueChanged += OnSoundSyncIntervalChanged;
        RecreatePlayerModelCheckTimer(SoundSyncInterval.Value);
        
        foreach (string volumeCommandName in _pluginConfig.VolumeCommandNames)
        {
            AddCommand(volumeCommandName, "", CommandDsVolume);
        }
        
        foreach (string toggleCommandName in _pluginConfig.ToggleCommandNames)
        {
            AddCommand(toggleCommandName, "", CommandDsToggle);
        }
    }
    
    public override void Unload(bool hotReload)
    {
        UnhookUserMessage(208, HookSound);
        
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        RemoveListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        
        SoundSyncInterval.ValueChanged -= OnSoundSyncIntervalChanged;
        
        foreach (string volumeCommandName in _pluginConfig.VolumeCommandNames)
        {
            RemoveCommand(volumeCommandName, CommandDsVolume);
        }
        
        foreach (string toggleCommandName in _pluginConfig.ToggleCommandNames)
        {
            RemoveCommand(toggleCommandName, CommandDsToggle);
        }
    }

    private void OnSoundSyncIntervalChanged(object? conVar, float newValue)
    {
        RecreatePlayerModelCheckTimer(newValue);
    }
    
    
    #region Commands
    
    // ==================================
    // Commands
    // ==================================


    private void CommandDsVolume(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (info.ArgCount > 2)
        {
            player.PrintToChat("Usage: ds_volume <volume>");
            return;
        }

        ReadOnlySpan<char> argVolume = info.ArgByIndex(1).AsSpan();

        if (!float.TryParse(argVolume.ToString(), out float volume))
        {
            player.PrintToChat($"Invalid argument {argVolume.ToString()}");
            return;
        }
            
        SetPlayerDsVolume(player, volume);
    }

    private void CommandDsToggle(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        TogglePlayerDsSound(player);
    }

    #endregion
    
    #region Player Settings Related
    
    // ==================================
    // Player Settings Related
    // ==================================
    
    private void SetPlayerDsVolume(CCSPlayerController player, float volume)
    {
        _dsPlayers[player.Slot].SoundVolume = volume;
        player.PrintToChat($"Set volume to {volume}");
        // TODO Save to database
    }

    private void TogglePlayerDsSound(CCSPlayerController player)
    {
        var muted = _dsPlayers[player.Slot].IsSoundMuted;
        
        _dsPlayers[player.Slot].IsSoundMuted = !muted;
        
        player.PrintToChat($"Sound now {muted}");
    }
    
    private void InitPlayerDsSettings(int slot)
    {
        // TODO get player preference from DB
        _dsPlayers[slot] = new DsPlayer
        {
            SoundVolume = 0.3F,
            IsSoundMuted = false
        };
    }
    
    #endregion
    
    
    #region hooks
    
    // ==================================
    // Hooks
    // ==================================
    
    private HookResult HookSound(UserMessage msg)
    {
        uint soundEventHash = msg.ReadUInt("soundevent_hash");

        // Ignore sounds when defined in IgnoringDamageHashes
        if (Enum.IsDefined(typeof(IgnoringDamageHashes), soundEventHash))
        {
            msg.SetInt("soundevent_guid", 0);
            msg.SetInt("soundevent_hash", 0);
        }
        
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var victim = @event.Userid;

        if (victim == null)
            return HookResult.Continue;

        if (!_dsPlayers.TryGetValue(victim.Slot, out var dsPlayer))
            return HookResult.Continue;

        // If player died recently
        if (dsPlayer.NextDamageSoundAvailableTime - Server.CurrentTime > 0.0F)
            return HookResult.Continue;
        
        dsPlayer.NextDamageSoundAvailableTime = Server.CurrentTime + DeathSoundPlaybackCooldown.Value;
        
        PlaySoundToPlayer(dsPlayer.DamageSound, victim);
        
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;

        if (victim == null)
            return HookResult.Continue;

        if (!_dsPlayers.TryGetValue(victim.Slot, out var dsPlayer))
            return HookResult.Continue;

        // If player died recently
        if (dsPlayer.NextDeathSoundAvailableTime - Server.CurrentTime > 0.0F)
            return HookResult.Continue;
        
        dsPlayer.NextDeathSoundAvailableTime = Server.CurrentTime + DeathSoundPlaybackCooldown.Value;
        
        PlaySoundToPlayer(dsPlayer.DeathSound, victim);
        
        return HookResult.Continue;
    }

    private void OnClientPutInServer(int slot)
    {
        InitPlayerDsSettings(slot);
    }

    #endregion
    
    
    #region Helpers
    
    // ==================================
    // Helpers
    // ==================================
    
    private void PlaySoundToPlayer(in string soundName, CBaseEntity soundSource)
    {
        if (string.IsNullOrEmpty(soundName))
            return;
        
        foreach (CCSPlayerController player in Utilities.GetPlayers())
        {
            if (!_pluginConfig.BotsEmitSound && player.IsBot || player.IsHLTV)
                continue;
            
            if (_dsPlayers[player.Slot].IsSoundMuted)
                continue;
            
            soundSource.EmitSound(soundName, new RecipientFilter(player), _dsPlayers[player.Slot].SoundVolume);
        }
    }

    private void RecreatePlayerModelCheckTimer(float timerInterval)
    {
        _modelCheckTimer?.Kill();

        _modelCheckTimer = AddTimer(timerInterval, () =>
        {
            foreach (CCSPlayerController player in Utilities.GetPlayers())
            {
                if (!_pluginConfig.BotsEmitSound && player.IsBot || player.IsHLTV)
                    continue;

                var playerPawn = player.PlayerPawn.Value;
                
                if (playerPawn == null)
                    continue;
                
                if (playerPawn.CBodyComponent?.SceneNode == null)
                    continue;


                RefreshPlayerDamageSound(player, playerPawn.CBodyComponent.SceneNode.GetSkeletonInstance().ModelState.ModelName);
            }
        }, TimerFlags.REPEAT);
    }
    
    private void RefreshPlayerDamageSound(CCSPlayerController player, in string modelName)
    {
        if (_pluginConfig.DamageSounds.TryGetValue(modelName, out var damageSound))
        {
            _dsPlayers[player.Slot].DamageSound = damageSound.DamageSoundName;
            _dsPlayers[player.Slot].DeathSound = damageSound.DeathSoundName;
        }
        else
        {
            _dsPlayers[player.Slot].DamageSound = string.Empty;
            _dsPlayers[player.Slot].DeathSound = string.Empty;
        }
    }
    
    #endregion
}