namespace DamageSound.Database.Repositories.SqlProviders.Interfaces;

public interface IDsPlayerPreferenceQueries
{
    string TableName { get; }
    
    string EnsureTableExistsSql();
    
    string UpsertPlayerPreferenceSql();
    
    string GetPlayerPreferenceSql();
}