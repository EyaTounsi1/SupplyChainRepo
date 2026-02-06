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

            var account = cfg["Account"];
            var user = cfg["User"];
            var warehouse = cfg["Warehouse"];
            var role = cfg["Role"];
            var database = cfg["Database"];
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

            if (!Path.IsPathRooted(keyPath))
                keyPath = Path.GetFullPath(keyPath);

            if (!File.Exists(keyPath))
                throw new FileNotFoundException($"Snowflake private key file not found: {keyPath}");

            var keyText = await File.ReadAllTextAsync(keyPath);
            keyText = keyText.ReplaceLineEndings("\n");

            var csb = new SnowflakeDbConnectionStringBuilder
            {
                ["ACCOUNT"] = account,
                ["HOST"] = $"{account}.snowflakecomputing.com",
                ["USER"] = user,
                ["AUTHENTICATOR"] = "snowflake_jwt",
                ["PRIVATE_KEY"] = keyText,
                ["PRIVATE_KEY_PWD"] = passphrase,

                // Keep these, but don't rely on them alone
                ["WAREHOUSE"] = warehouse,
                ["ROLE"] = role
            };

            using var conn = new SnowflakeDbConnection
            {
                ConnectionString = csb.ConnectionString
            };

            await conn.OpenAsync();

            // ✅ Make session context deterministic
            await SetSessionContextAsync(conn, role, warehouse, database, null);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }

       private static async Task SetSessionContextAsync(
    SnowflakeDbConnection conn,
    string role,
    string warehouse,
    string? database,
    string? schema)
{
    await ExecNonQueryAsync(conn, $"USE ROLE {QuoteIdent(role)}");

    // ⭐ KEY: match UI behavior in many orgs
    await ExecNonQueryAsync(conn, "USE SECONDARY ROLES ALL");

    await ExecNonQueryAsync(conn, $"USE WAREHOUSE {QuoteIdent(warehouse)}");

    if (!string.IsNullOrWhiteSpace(database))
        await ExecNonQueryAsync(conn, $"USE DATABASE {QuoteIdent(database)}");

    if (!string.IsNullOrWhiteSpace(schema))
        await ExecNonQueryAsync(conn, $"USE SCHEMA {QuoteIdent(schema)}");
}


        private static async Task ExecNonQueryAsync(SnowflakeDbConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private static string QuoteIdent(string ident)
        {
            return "\"" + ident.Replace("\"", "\"\"") + "\"";
        }
    }
}
