using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using DamageSound.Config;
using DamageSound.Database;
using DamageSound.Models;
using DamageSound.SoundHashes;
using Microsoft.Extensions.Logging;
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
    private DsDatabaseProvider _dsDatabaseProvider = null!;
    
    private const float VolumeMin = 0.0F;
    private const float VolumeMax = 100.0F;
    
    
    private Timer? _modelCheckTimer;
    
    public override void Load(bool hotReload)
    {
        _pluginConfig = new ConfigParser(Path.Combine(ModuleDirectory, "configs", "dsconfig.toml"), this).Load();
        _dsDatabaseProvider = new DsDatabaseProvider(_pluginConfig.DatabaseConfig, this);
        
        
        HookUserMessage(208, HookSound, HookMode.Pre);
        
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

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
        RemoveListener<Listeners.OnClientDisconnect>(OnClientDisconnect); 
        
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

        if (info.ArgCount < 2)
        {
            player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "DamageSound.Command.Volume.Usage", info.ArgByIndex(0))).ToString());
            if (_dsPlayers.TryGetValue(player.Slot, out var dsPlayer))
            {
                player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "DamageSound.Command.Volume.Current", $"{dsPlayer.SoundVolume*100:F0}")).ToString());
            }
            return;
        }

        ReadOnlySpan<char> argVolume = info.ArgByIndex(1).AsSpan();

        if (!float.TryParse(argVolume, out float volume))
        {
            player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "General.Command.InvalidArgument", argVolume.ToString())).ToString());
            return;
        }

        if (volume < 0.0F || volume > 100.0F)
        {
            player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "General.Command.OutOfRange", volume, VolumeMin, VolumeMax)).ToString());
            return;
        }
            
        // divide with 100, because command argument is percentage
        SetPlayerDsVolume(player, volume/100);
        player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "DamageSound.Command.Volume.Set", $"{volume:F0}")).ToString());
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
    }

    private void TogglePlayerDsSound(CCSPlayerController player)
    {
        var muted = _dsPlayers[player.Slot].IsSoundMuted;
        
        _dsPlayers[player.Slot].IsSoundMuted = !muted;
        
        var word = muted ? "Word.Enabled" : "Word.Disabled";
        var translatedWord = LocalizeText(player, word);
        player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, "DamageSound.Command.SoundToggled", translatedWord.ToString())).ToString());
    }
    
    private void InitPlayerDsSettings(int slot)
    {
        _dsPlayers[slot] = new DsPlayer
        {
            SoundVolume = 0.3F,
            IsSoundMuted = false
        };
        
        CCSPlayerController? player = Utilities.GetPlayerFromSlot(slot);
        
        if (player == null)
            return;
        
        if (player.IsBot || player.IsHLTV)
            return;
        
        SteamID steamId = (SteamID)player.SteamID;
        _dsPlayers[slot].SteamId = steamId;
        _ = _dsDatabaseProvider.PlayerPreferenceRepository.GetPlayerPreferences(steamId, _dsPlayers[slot]).ConfigureAwait(false);
    }
    
    #endregion
    
    
    #region hooks
    
    // ==================================
    // Hooks
    // ==================================
    
    private HookResult HookSound(UserMessage msg)
    {
        uint soundEventHash = msg.ReadUInt("soundevent_hash");
        int sourceEntityIndex = msg.ReadInt("source_entity_index");

        if (sourceEntityIndex == -1)
            return HookResult.Continue;
        
        // Ignore sounds when defined in IgnoringDamageHashes
        if (Enum.IsDefined(typeof(IgnoringDamageHashes), soundEventHash))
        {
            if (!_dsPlayers.TryGetValue(sourceEntityIndex, out var dsPlayer))
                return HookResult.Continue;

            // If player is muted a damage/death sound, or player model doesn't have a sounds, then play sound normally
            if (dsPlayer.IsSoundMuted || string.IsNullOrEmpty(dsPlayer.DamageSound) || string.IsNullOrEmpty(dsPlayer.DeathSound))
                return HookResult.Continue;
            
            msg.SetInt("soundevent_guid", 0);
            msg.SetInt("soundevent_hash", 0);
        }
        
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;

        if (victim == null)
            return HookResult.Continue;

        if (!_dsPlayers.TryGetValue(victim.Slot, out var dsPlayer))
            return HookResult.Continue;

        // If player take damage recently
        if (dsPlayer.NextDamageSoundAvailableTime - Server.CurrentTime > 0.0F)
            return HookResult.Continue;
        
        dsPlayer.NextDamageSoundAvailableTime = Server.CurrentTime + DamageSoundPlaybackCooldown.Value;
        
        Server.NextFrame(() =>
        {
            if (victim.PlayerPawn.Value?.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                return;
            
            PlaySoundToPlayer(dsPlayer.DamageSound, victim);
        });
        
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? victim = @event.Userid;

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

    private void OnClientDisconnect(int slot)
    {
        if (!_dsPlayers.TryGetValue(slot, out var dsPlayer))
            return;
        
        var steamId = dsPlayer.SteamId;
        
        if (steamId == null)
            return;
        
        SavePlayerPrefsToDatabase(steamId, dsPlayer);
        _dsPlayers.Remove(slot);
    }

    #endregion
    
    
    #region Helpers
    
    // ==================================
    // Helpers
    // ==================================
    
    private void PlaySoundToPlayer(string soundName, CBaseEntity soundSource)
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
    
    private void RefreshPlayerDamageSound(CCSPlayerController player, string modelName)
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

    private void PrintLocalizedChatWithPrefixToPlayer(CCSPlayerController? player, string translationKey, params object[] args)
    {
        if (player == null)
        {
            Server.PrintToConsole($"{LocalizeText(player, translationKey, args)}");
            return;
        }
        
        player.PrintToChat(GetTextWithPluginPrefix(player, LocalizeText(player, translationKey, args)).ToString());
    }

    private ReadOnlySpan<char> GetTextWithPluginPrefix(CCSPlayerController player, ReadOnlySpan<char> text)
    {
        return $"{Localizer.ForPlayer(player, "Plugin.Prefix")} {text}".AsSpan();
    }

    private ReadOnlySpan<char> LocalizeText(CCSPlayerController? player, string translationKey, params object[] args)
    {
        if (player == null)
        {
            return Localizer[translationKey, args].Value.AsSpan();
        }

        return Localizer.ForPlayer(player, translationKey, args).AsSpan();
    }
    
    private void SavePlayerPrefsToDatabase(SteamID steamId, DsPlayer playerPref)
    {
        _ = _dsDatabaseProvider.PlayerPreferenceRepository.UpsertPlayerPreferences(steamId, playerPref).ConfigureAwait(false);
    }
    #endregion
}