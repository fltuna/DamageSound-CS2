using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using DamageSound.Config;
using DamageSound.Database.Repositories.SqlProviders.Interfaces;
using DamageSound.Database.Repositories.SqlProviders.Sqlite;
using DamageSound.Models;
using Dapper;
using Microsoft.Extensions.Logging;

namespace DamageSound.Database.Repositories;

public class PlayerPreferenceRepository
{
    
    private readonly IDsSqlQueryProvider _sqlQueryProvider;
    private readonly string _connectionString;
    private readonly BasePlugin _plugin;
    
    public PlayerPreferenceRepository(string connectionString, DbType databaseType, BasePlugin plugin)
    {
        _sqlQueryProvider = CreateSqlQueryProvider(databaseType);
        _connectionString = connectionString;
        _plugin = plugin;
        
        EnsureTablesExists();
    }

    private void EnsureTablesExists()
    {
        try
        {
            using var connection = _sqlQueryProvider.CreateConnection(_connectionString);
            connection.Open();
            connection.Execute(_sqlQueryProvider.PlayerPreferenceQueries.EnsureTableExistsSql());
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Failed to create {TableName} table", _sqlQueryProvider.PlayerPreferenceQueries.TableName);
            throw;
        }
        _plugin.Logger.LogInformation("{TableName} table ensured", _sqlQueryProvider.PlayerPreferenceQueries.TableName);
    }

    
    public async Task UpsertPlayerPreferences(SteamID steamId, DsPlayer playerPrefs)
    {
        try
        {
            _plugin.Logger.LogInformation("Updating player {SteamID} DamageSound preferences to: Volume - {SoundVolume}, IsEnabled - {IsMuted}", steamId.SteamId64, playerPrefs.SoundVolume, playerPrefs.IsSoundMuted);
            
            using var connection = _sqlQueryProvider.CreateConnection(_connectionString);
            connection.Open();

            await connection.ExecuteAsync(
                _sqlQueryProvider.PlayerPreferenceQueries.UpsertPlayerPreferenceSql(), 
                new { SteamID = steamId.SteamId64, SoundVolume = playerPrefs.SoundVolume, IsMuted = playerPrefs.IsSoundMuted }
            );
            
            _plugin.Logger.LogInformation("Successfully updated preferences for player {SteamID}", steamId.SteamId64);
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Error while updating player preferences for {SteamID}", steamId.SteamId64);
            throw;
        }
    }
    
    public async Task GetPlayerPreferences(SteamID steamId, DsPlayer playerPrefs)
    {
        try
        {
            _plugin.Logger.LogInformation("Getting player {SteamID} preferences from database", steamId.SteamId64);
        
            using var connection = _sqlQueryProvider.CreateConnection(_connectionString);
            connection.Open();

            var result = await connection.QueryFirstOrDefaultAsync<PlayerPreferenceResult>(
                _sqlQueryProvider.PlayerPreferenceQueries.GetPlayerPreferenceSql(), 
                new { SteamID = steamId.SteamId64 }
            );
        
            if (result != null)
            {
                playerPrefs.SoundVolume = result.SoundVolume;
                playerPrefs.IsSoundMuted = result.IsSoundMuted;
            
                _plugin.Logger.LogInformation("Successfully retrieved preferences for player {SteamID}: Volume - {SoundVolume}, IsEnabled - {IsMuted}", 
                    steamId.SteamId64, playerPrefs.SoundVolume, playerPrefs.IsSoundMuted);
            }
            else
            {
                _plugin.Logger.LogWarning("No preferences found for player {SteamID}, using default values", steamId.SteamId64);
            }
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogError(ex, "Error while getting player preferences for {SteamID}", steamId.SteamId64);
            throw;
        }
    }
    
    
    private IDsSqlQueryProvider CreateSqlQueryProvider(DbType dbType)
    {
        return dbType switch
        {
            DbType.Sqlite => new SqliteSqlProvider(),
            DbType.MySql => throw new NotImplementedException("MySQL is not implemented"),
            DbType.PostgreSql => throw new NotImplementedException("PostgreSQL is not implemented"),
            _ => throw new InvalidOperationException("Database is unsupported"),
        };
    }
    
    
    

    private class PlayerPreferenceResult
    {
        public float SoundVolume { get; set; }
        public bool IsSoundMuted { get; set; }
    }
}