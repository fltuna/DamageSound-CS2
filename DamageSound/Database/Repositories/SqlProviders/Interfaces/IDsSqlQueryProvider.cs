using System.Data;

namespace DamageSound.Database.Repositories.SqlProviders.Interfaces;

public interface IDsSqlQueryProvider
{
    public IDsPlayerPreferenceQueries PlayerPreferenceQueries { get; }
    
    public IDbConnection CreateConnection(string connectionString);
}