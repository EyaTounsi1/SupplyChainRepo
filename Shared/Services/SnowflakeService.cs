using Snowflake.Data.Client;
using System.Data;
namespace PartTracker.Shared.Services
{
    public class SnowflakeService
    {
        private readonly IConfiguration _config;
        public SnowflakeService(IConfiguration config)
        {
            _config = config;
        }
        public async Task<DataTable> QueryAsync(string sql)
        {
            var cfg = _config.GetSection("Snowflake");
            // 1) Resolve key path
            var keyPath = cfg["PrivateKeyPath"];
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new InvalidOperationException("Snowflake:PrivateKeyPath missing in appsettings.json");
            if (!Path.IsPathRooted(keyPath))
                keyPath = Path.GetFullPath(keyPath);   // e.g. C:\...\vctlog\Keys\rsa_key.p8
            // 2) Read key text and normalise line endings
            var keyText = await File.ReadAllTextAsync(keyPath);
            keyText = keyText.ReplaceLineEndings("\n");
            var passphrase = cfg["PrivateKeyPassphrase"];
            if (string.IsNullOrEmpty(passphrase))
                throw new InvalidOperationException("Snowflake:PrivateKeyPassphrase missing in appsettings.json");
            // 3) Build connection string using the official properties
            var csb = new SnowflakeDbConnectionStringBuilder
            {
                ["ACCOUNT"] = cfg["Account"],                           // ← "VOLVOCARS-MANUFACTURINGANALYTICS"
                ["HOST"] = $"{cfg["Account"]}.snowflakecomputing.com", 
                ["USER"] = cfg["User"],
                ["WAREHOUSE"] = cfg["Warehouse"],
                ["ROLE"] = cfg["Role"],
                ["AUTHENTICATOR"] = "snowflake_jwt",       
                ["PRIVATE_KEY"] = keyText,              
                ["PRIVATE_KEY_PWD"] = passphrase      
            };
            using var conn = new SnowflakeDbConnection
            {
                ConnectionString = csb.ConnectionString
            };
            await conn.OpenAsync();  // if key+passphrase are wrong, it will fail here
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }
    }
}