using System.Runtime.InteropServices;
using System.Security;
using CounterStrikeSharp.API.Core;
using DamageSound.Config;
using DamageSound.Database.Repositories;
using Microsoft.Extensions.Logging;

namespace DamageSound.Database;

public class DsDatabaseProvider
{
    private readonly BasePlugin _plugin;
    private readonly DatabaseConfig _databaseConfig; 
    private string _connectionString = string.Empty;
    private DbType _dbType = DbType.Sqlite;

    public PlayerPreferenceRepository PlayerPreferenceRepository { get; private set; }

    public DsDatabaseProvider(DatabaseConfig databaseConfig, BasePlugin plugin)
    {
        _databaseConfig = databaseConfig;
        _plugin = plugin;
        
        
        ConfigureDatabase();
        
        PlayerPreferenceRepository = new PlayerPreferenceRepository(_connectionString, _dbType, _plugin);
    }

    private void ConfigureDatabase()
    {
        switch (_databaseConfig.DatabaseType)
        {
            case DbType.MySql:
                _dbType = DbType.MySql;    
                string passwordMySql = ConvertSecureStringToString(_databaseConfig.DatabasePassword);

                try
                {
                    _connectionString = $"Server={_databaseConfig.DatabaseHost};" +
                                        $"Database={_databaseConfig.DatabaseName};" +
                                        $"User Id={_databaseConfig.DatabaseUser};" +
                                        $"Password={passwordMySql};" +
                                        $"Port={_databaseConfig.DatabasePort};";
                    
                    _plugin.Logger.LogInformation("Using MySQL database at host: {DatabaseHost}, Database Name: {DatabaseName}", _databaseConfig.DatabaseHost, _databaseConfig.DatabaseName);
                }
                finally
                {
                    // Clear password from memory ASAP
                    SecurelyEraseString(ref passwordMySql);
                }
                
                break;
                
            
            case DbType.PostgreSql:
                _dbType = DbType.PostgreSql;
                string passwordPostgreSql = ConvertSecureStringToString(_databaseConfig.DatabasePassword);
    
                try
                {
                    _connectionString = $"Host={_databaseConfig.DatabaseHost};" +
                                        $"Database={_databaseConfig.DatabaseName};" +
                                        $"Username={_databaseConfig.DatabaseUser};" +
                                        $"Password={passwordPostgreSql};" +
                                        $"Port={_databaseConfig.DatabasePort};";
        
                    _plugin.Logger.LogInformation("Using PostgreSql database at host: {DatabaseHost}, Database Name: {DatabaseName}", _databaseConfig.DatabaseHost, _databaseConfig.DatabaseName);
                }
                finally
                {
                    SecurelyEraseString(ref passwordPostgreSql);
                }
                
                break;
                
            
            case DbType.Sqlite:
            default:
                _dbType = DbType.Sqlite;
                string dbPath = Path.Combine(_plugin.ModuleDirectory, _databaseConfig.DatabaseName);
                _connectionString = $"Data Source={dbPath}";
                _plugin.Logger.LogInformation("Using SQLite database at {DatabasePath}", dbPath);
                break;
        }
    }
    
    
    
    private string ConvertSecureStringToString(SecureString secureString)
    {
        IntPtr bstrPtr = Marshal.SecureStringToBSTR(secureString);
        try
        {
            return Marshal.PtrToStringBSTR(bstrPtr);
        }
        finally
        {
            Marshal.ZeroFreeBSTR(bstrPtr);
        }
    }
    
    private void SecurelyEraseString(ref string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        
        GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
    
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            int size = value.Length * sizeof(char);
        
            Marshal.Copy(new byte[size], 0, ptr, size);
        }
        finally
        {
            handle.Free();
        
            value = "";
        }
    }
}