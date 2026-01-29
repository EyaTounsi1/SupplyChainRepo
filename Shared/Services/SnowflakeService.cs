using Snowflake.Data.Client;
using System.Data;
using Microsoft.Extensions.Configuration;
 
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
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be empty.", nameof(sql));
 
            var cfg = _config.GetSection("Snowflake");
            if (!cfg.Exists())
                throw new InvalidOperationException("Missing 'Snowflake' section in appsettings.json");
 
            // Read required config
            var account = cfg["Account"];
            var user = cfg["User"];
            var warehouse = cfg["Warehouse"];
            var role = cfg["Role"];
            var database = cfg["Database"]; // optional but recommended
            var schema = cfg["Schema"];     // optional but recommended
            var keyPath = cfg["PrivateKeyPath"];
            var passphrase = cfg["PrivateKeyPassphrase"];
 
            if (string.IsNullOrWhiteSpace(account))
                throw new InvalidOperationException("Snowflake:Account missing in appsettings.json");
            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("Snowflake:User missing in appsettings.json");
            if (string.IsNullOrWhiteSpace(warehouse))
                throw new InvalidOperationException("Snowflake:Warehouse missing in appsettings.json");
            if (string.IsNullOrWhiteSpace(role))
                throw new InvalidOperationException("Snowflake:Role missing in appsettings.json");
            if (string.IsNullOrWhiteSpace(keyPath))
                throw new InvalidOperationException("Snowflake:PrivateKeyPath missing in appsettings.json");
            if (string.IsNullOrWhiteSpace(passphrase))
                throw new InvalidOperationException("Snowflake:PrivateKeyPassphrase missing in appsettings.json");
 
            // Resolve key path (relative → absolute)
            if (!Path.IsPathRooted(keyPath))
                keyPath = Path.GetFullPath(keyPath);
 
            if (!File.Exists(keyPath))
                throw new FileNotFoundException($"Snowflake private key file not found: {keyPath}");
 
            // Read key and normalize line endings
            var keyText = await File.ReadAllTextAsync(keyPath);
            keyText = keyText.ReplaceLineEndings("\n");
 
            // Build connection string
            var csb = new SnowflakeDbConnectionStringBuilder
            {
                ["ACCOUNT"] = account,
                ["HOST"] = $"{account}.snowflakecomputing.com",
                ["USER"] = user,
                ["WAREHOUSE"] = warehouse,
                ["ROLE"] = role,
                ["AUTHENTICATOR"] = "snowflake_jwt",
                ["PRIVATE_KEY"] = keyText,
                ["PRIVATE_KEY_PWD"] = passphrase
            };
 
            using var conn = new SnowflakeDbConnection
            {
                ConnectionString = csb.ConnectionString
            };
 
            await conn.OpenAsync();
 
            // Set session context so CURRENT_DATABASE()/CURRENT_SCHEMA() are not blank
            await SetSessionContextAsync(conn, database, schema);
 
            // Execute the query and return DataTable
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
 
            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }
 
        private static async Task SetSessionContextAsync(SnowflakeDbConnection conn, string? database, string? schema)
        {
            // IMPORTANT: USE DATABASE must run before USE SCHEMA
            if (!string.IsNullOrWhiteSpace(database))
            {
                using var useDb = conn.CreateCommand();
                useDb.CommandText = $"USE DATABASE {QuoteIdent(database)}";
                await useDb.ExecuteNonQueryAsync();
            }
 
            if (!string.IsNullOrWhiteSpace(schema))
            {
                using var useSchema = conn.CreateCommand();
                useSchema.CommandText = $"USE SCHEMA {QuoteIdent(schema)}";
                await useSchema.ExecuteNonQueryAsync();
            }
        }
 
        private static string QuoteIdent(string ident)
        {
            // Snowflake identifier quoting: "NAME" and escape internal quotes.
            return "\"" + ident.Replace("\"", "\"\"") + "\"";
        }
    }
}