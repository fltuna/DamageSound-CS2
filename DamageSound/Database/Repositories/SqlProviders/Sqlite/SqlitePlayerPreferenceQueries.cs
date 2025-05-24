using DamageSound.Database.Repositories.SqlProviders.Interfaces;

namespace DamageSound.Database.Repositories.SqlProviders.Sqlite;

public class SqlitePlayerPreferenceQueries: IDsPlayerPreferenceQueries
{
    public string TableName => "DamageSoundPlayerPreferences";

    public string EnsureTableExistsSql() => $@"
        CREATE TABLE IF NOT EXISTS {TableName} (
            steam_id INTEGER PRIMARY KEY,
            sound_volume REAL NOT NULL DEFAULT 1.0,
            is_sound_muted INTEGER NOT NULL DEFAULT 0
        );";

    public string UpsertPlayerPreferenceSql() => $@"
        INSERT INTO {TableName} (steam_id, sound_volume, is_sound_muted) 
        VALUES (@SteamID, @SoundVolume, @IsMuted)
        ON CONFLICT(steam_id) DO UPDATE SET 
            sound_volume = excluded.sound_volume,
            is_sound_muted = excluded.is_sound_muted;";

    public string GetPlayerPreferenceSql() => $@"
        SELECT sound_volume AS SoundVolume, is_sound_muted AS IsSoundMuted 
        FROM {TableName} 
        WHERE steam_id = @SteamID;";
}