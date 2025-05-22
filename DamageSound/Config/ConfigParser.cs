using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using DamageSound.Models;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

namespace DamageSound.Config;

public sealed class ConfigParser(string configPath, BasePlugin plugin)
{
    private string ConfigPath { get; } = configPath;
    private BasePlugin Plugin { get; } = plugin;
    
    
    public PluginConfig Load()
    {
        string directory = Path.GetDirectoryName(ConfigPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        if (File.Exists(ConfigPath))
        {
            return LoadConfigFromFile();
        }
        
        // If there is no *.toml files in config directory, then create default config.
        if (Directory.GetFiles(directory, "*.toml", SearchOption.AllDirectories).Length == 0)
        {
            WriteDefaultConfig();
        }
        
        return LoadConfigFromFile();
    }

    private PluginConfig LoadConfigFromFile()
    {
        string configText;

        // If file exists, then load from file.
        if (File.Exists(ConfigPath))
        {
            configText = File.ReadAllText(ConfigPath);
        }
        else
        {
            throw new FileNotFoundException($"Config file not found: {ConfigPath}");
        }
        
        // It corresponds with -> Model Path - SoundData
        Dictionary<string, SoundData> soundData = new(StringComparer.OrdinalIgnoreCase);
        
        TomlTable toml = Toml.ToModel(configText);

        
        (List<string> VolumeCommandNames, List<string> ToggleCommandNames, bool BotsEmitSound, string SoundFilePath) 
            pluginSettings = 
                (new(), new(), false, SoundFilePath: string.Empty);

        int processedDataCounts = 0;
        foreach (var (key, value) in toml)
        {
            if (value is not TomlTable soundDataTable)
                continue;
            
            if (key.Equals("Settings", StringComparison.OrdinalIgnoreCase))
            {
                var missing = ParsePluginSetting(soundDataTable, ref pluginSettings);
                
                if (!missing.Any())
                    continue;
                else
                    throw new InvalidOperationException($"Required plugin settings are missing: {string.Join(", ", missing)}");
            }

            var newData = CreateSoundData(key, soundDataTable);
            
            if (newData == null)
                continue;
            
            soundData[newData.ModelPath] = newData;
            processedDataCounts++;
        }
        
        Plugin.Logger.LogInformation("Successfully loaded {Count} sound data from config files.", processedDataCounts);
        return new PluginConfig(soundData, pluginSettings.BotsEmitSound, pluginSettings.ToggleCommandNames, pluginSettings.VolumeCommandNames, pluginSettings.SoundFilePath);
    }

    private List<string> ParsePluginSetting(TomlTable pluginSettingTable, ref (List<string> VolumeCommandNames, List<string> ToggleCommandNames, bool BotsEmitSound, string SoundFilePath) pluginSettings)
    {
        List<string> missingSettings = new();

        if (pluginSettingTable.TryGetValue("BotEmitsSound", out var botEmitsSoundObj) && botEmitsSoundObj is bool botEmitsSound)
        {
            pluginSettings.BotsEmitSound = botEmitsSound;
        }
        else
        {
            missingSettings.Add("BotEmitsSound");
        }


        if (pluginSettingTable.TryGetValue("SoundFilePath", out var soundFilePathObj) && soundFilePathObj is string soundFilePath)
        {
            pluginSettings.SoundFilePath = soundFilePath;
        }
        else
        {
            missingSettings.Add("SoundFilePath");
        }


        if (pluginSettingTable.TryGetValue("VolumeCommandName", out var volumeCommandNamesObj) && volumeCommandNamesObj is TomlArray volumeCommandNames)
        {
            pluginSettings.VolumeCommandNames.AddRange(ParseStringArray(volumeCommandNames));
        }
        else
        {
            missingSettings.Add("VolumeCommandName");
        }

        if (pluginSettingTable.TryGetValue("ToggleCommandName", out var toggleCommandNamesObj) && toggleCommandNamesObj is TomlArray toggleCommandNames)
        {
            pluginSettings.ToggleCommandNames.AddRange(ParseStringArray(toggleCommandNames));
        }
        else
        {
            missingSettings.Add("ToggleCommandName");
        }
        
        return missingSettings;
    }
    
    private SoundData? CreateSoundData(string key, TomlTable soundDataTable)
    {
        if (!soundDataTable.TryGetValue("ModelPath", out var modelPathObj) || modelPathObj is not string modelPath)
        {
            Plugin.Logger.LogWarning("{SectionName} is missing required value: ModelPath", key);
            return null;
        }

        if (string.IsNullOrEmpty(modelPath))
        {
            Plugin.Logger.LogWarning("{SectionName}'s ModelPath is empty! skipping!", key);
            return null;
        }

        
        if (!soundDataTable.TryGetValue("DamageSound", out var damageSoundObj) || damageSoundObj is not string damageSoundPath)
        {
            Plugin.Logger.LogWarning("{SectionName} is missing required value: DamageSound", key);
            return null;
        }

        if (string.IsNullOrEmpty(damageSoundPath))
        {
            Plugin.Logger.LogWarning("{SectionName}'s DamageSound is empty! skipping!", key);
            return null;
        }

        
        if (!soundDataTable.TryGetValue("DeathSound", out var deathSoundObj) || deathSoundObj is not string deathSoundPath)
        {
            Plugin.Logger.LogWarning("{SectionName} is missing required value: DeathSound", key);
            return null;
        }

        if (string.IsNullOrEmpty(deathSoundPath))
        {
            Plugin.Logger.LogWarning("{SectionName}'s DeathSound is empty! skipping!", key);
            return null;
        }
        
        return new SoundData(key, modelPath, damageSoundPath, deathSoundPath);
    }
    
    
    private static void ProcessDirectory(string directoryPath, StringBuilder combinedContent)
    {
        foreach (string filePath in Directory.GetFiles(directoryPath, "*.toml"))
        {
            try
            {
                string content = File.ReadAllText(filePath);

                if (combinedContent.Length > 0 && !combinedContent.ToString().EndsWith(Environment.NewLine))
                {
                    combinedContent.AppendLine();
                }

                combinedContent.Append(content);

                if (!content.EndsWith(Environment.NewLine))
                {
                    combinedContent.AppendLine();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read file {filePath}: {ex.Message}", ex);
            }
        }

        // Recursive directory processing
        foreach (string subDirectoryPath in Directory.GetDirectories(directoryPath))
        {
            ProcessDirectory(subDirectoryPath, combinedContent);
        }
    }
    
    
    private List<string> ParseStringArray(TomlArray array)
    {
        List<string> result = new List<string>();
        foreach (var item in array)
        {
            if (item is string stringValue)
            {
                result.Add(stringValue);
            }
        }
        return result;
    }

    private void WriteDefaultConfig()
    {
        const string defaultConfig = @"
[Settings]
# Should bot emits damage/death sound?
BotEmitsSound = false

# Volume command name, e.g. !ds_volume, css_ds_volume
VolumeCommandName = [""ds_volume"", ""dsv""]

# Toggle command name, e.g. !ds_toggle, css_ds_toggle
ToggleCommandName = [""ds_toggle"", ""dst""]

# Path to .vsndevts. file extension should be end with `.vsndevts` e.g. ""soundevents/damagesound.vsndevts""
# If you already precached a .vsndevts file in another plugin, then you can leave as blank.
SoundFilePath = """"


# You can set name to whatever you want, but it should be unique.
[CTDefault]
ModelPath = ""characters/models/ctm_sas/ctm_sas.vmdl""
DamageSound = ""Test.DamageSound""
DeathSound = ""Test.DeathSound""

[TDefault]
ModelPath = ""characters/models/tm_phoenix/tm_phoenix.vmdl""
DamageSound = ""Test.DamageSound2""
DeathSound = ""Test.DeathSound2""
";
        
        File.WriteAllText(ConfigPath, defaultConfig);
    }
}