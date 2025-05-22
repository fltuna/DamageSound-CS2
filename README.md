# DamageSound

Replaces existing damage sound to custom sound.


## How to find sound name from hash by bruteforce way

### I WILL NOT PROVIDE ANY SUPPORT ABOUT THIS ARTICLE. THIS IS FOR WHO CAN DEVELOP PLUGIN BY ITSELF.

This article describe the how to find desired sound's hash.

You can update sound hash yourself when hash is changed. (I think may not occur frequently)

### Pre Requirements

1. [Source 2 Viewer](https://valveresourceformat.github.io) for extract and decompile assets
2. Any IDE can modify C# files
3. .NET SDK for compile this plugin

### 1. Extract and decompile

Extract and decompile vsndevts_c located in `soundevents/` from pak01_dir.vpk using [Source 2 Viewer](https://valveresourceformat.github.io)

### 2. Move vsndevts file

Put vsndevts to `Tool/input` folder

### 3. Run extractor

Run `sound_keys_extractor.py` script with arguments like this: `python sound_keys_extractor.py -i "input/<name>.vsndevts" -o "SoundList<name>.cs`

### 4. Copy and paste program file

Check output and copy and paste to program source folder.

### 5. Modify test command

Then modify referenced list in test command function
```csharp
private void CmdTestSound(CCSPlayerController? player, CommandInfo info)
{
    if(player == null)
        return;
    
    
    isTesting = true;
    testClient = player;

    int currentIndex = 0;
    // Modify this to
    IReadOnlyList<string> list = SoundListXXXXXXX.SoundPaths;
    
    // like this
    IReadOnlyList<string> list = SoundListYYYYYYY.SoundPaths;
    
    int listSize = list.Count;
    // ..............
}
```

### 6. Compile

Compile plugin as usual.

Then move plugin file and reload server.

### 7. Execute test command

Execute `css_testsoundlist` in server and check console output.

### 8. Copy and paste the output file

Note: You should not include any output other than debug output.

Copy all console output and paste to text file, then remove unrelated outputs from text.

Text file should look like this
```
[client] WWWW.XXXX
[client] 123456789
[client] YYYY.ZZZZ
[client] 123456789
continues...
```

### 9. Run parser

Run `sound_log_parser.py` script with arguments like this: `python sound_keys_extractor.py -i "input/<name>.txt" -o "<name>SoundHashes.cs`

### 10. Remove SoundList<name>.cs

Remove `SoundList<name>.cs` from program source folder.

### 11. Almost done

When sound name is changed, you may need to modify source file.