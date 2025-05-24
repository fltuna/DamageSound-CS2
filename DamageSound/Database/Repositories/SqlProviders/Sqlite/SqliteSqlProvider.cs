using System.Data;
using System.Data.SQLite;
using DamageSound.Database.Repositories.SqlProviders.Interfaces;

namespace DamageSound.Database.Repositories.SqlProviders.Sqlite;

public class SqliteSqlProvider: IDsSqlQueryProvider
{
    public IDsPlayerPreferenceQueries PlayerPreferenceQueries { get; } = new SqlitePlayerPreferenceQueries();
    
    public IDbConnection CreateConnection(string connectionString) => 
        new SQLiteConnection(connectionString);
}