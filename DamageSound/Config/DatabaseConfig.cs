using System.Security;

namespace DamageSound.Config;

public class DatabaseConfig
{
    public DatabaseConfig(
        DbType databaseType,
        string databaseHost,
        string databasePort,
        string databaseName,
        string databaseUser,
        ref string databasePassword)
    {
        DatabaseType = databaseType;
        DatabaseHost = databaseHost;
        DatabasePort = databasePort;
        DatabaseName = databaseName;
        DatabaseUser = databaseUser;
        
        
        DatabasePassword = ConvertToSecureString(databasePassword);
        
        ClearString(ref databasePassword);
    }
    
    public DbType DatabaseType { get; }
    public string DatabaseHost { get; }
    public string DatabasePort { get; }
    public string DatabaseName { get; }
    public string DatabaseUser { get; }
    public SecureString DatabasePassword { get; }
    
    
    private SecureString ConvertToSecureString(string password)
    {
        var securePassword = new SecureString();

        if (string.IsNullOrEmpty(password))
        {
            securePassword.MakeReadOnly();
            return securePassword;
        }
        
        
        foreach (char c in password)
        {
            securePassword.AppendChar(c);
        }
        
        securePassword.MakeReadOnly();
        return securePassword;
    }

    
    private void ClearString(ref string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        int length = text.Length;
        
        char[] charArray = text.ToCharArray();
        
        text = null!;
        
        unsafe
        {
            fixed (char* ptr = charArray)
            {
                for (int i = 0; i < length; i++)
                {
                    ptr[i] = '\0';
                }
                // Prevent aggressive compiler optimization from removing the memory clearing operations.
                Thread.MemoryBarrier();
            }
        }
    }
}