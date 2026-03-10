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

        public async Task<DataTable> QueryAsync(string sql, string dbKey, string? schemaKey = null)
{
    if (string.IsNullOrWhiteSpace(sql))
        throw new ArgumentException("SQL query cannot be empty.", nameof(sql));

    var sf = _config.GetSection("Snowflake");
    if (!sf.Exists())
        throw new InvalidOperationException("Missing 'Snowflake' section in appsettings.json");

    var account = sf["Account"];
    var user = sf["User"];
    var warehouse = sf["Warehouse"];
    var role = sf["Role"];
    var keyPath = sf["PrivateKeyPath"];
    var passphrase = sf["PrivateKeyPassphrase"];

    var dbSection = sf.GetSection($"Databases:{dbKey}");
    if (!dbSection.Exists())
        throw new InvalidOperationException($"Missing Snowflake database config: Snowflake:Databases:{dbKey}");

    var database = dbSection["Database"];
    if (string.IsNullOrWhiteSpace(database))
        throw new InvalidOperationException($"Snowflake:Databases:{dbKey}:Database missing in appsettings.json");

    string? schema = null;
    if (!string.IsNullOrWhiteSpace(schemaKey))
    {
        schema = dbSection[$"Schemas:{schemaKey}"];
        if (string.IsNullOrWhiteSpace(schema))
            throw new InvalidOperationException($"Snowflake schema key not found: Snowflake:Databases:{dbKey}:Schemas:{schemaKey}");
    }

    // validate required fields
    if (string.IsNullOrWhiteSpace(account)) throw new InvalidOperationException("Snowflake:Account missing");
    if (string.IsNullOrWhiteSpace(user)) throw new InvalidOperationException("Snowflake:User missing");
    if (string.IsNullOrWhiteSpace(warehouse)) throw new InvalidOperationException("Snowflake:Warehouse missing");
    if (string.IsNullOrWhiteSpace(role)) throw new InvalidOperationException("Snowflake:Role missing");
    if (string.IsNullOrWhiteSpace(keyPath)) throw new InvalidOperationException("Snowflake:PrivateKeyPath missing");
    if (string.IsNullOrWhiteSpace(passphrase)) throw new InvalidOperationException("Snowflake:PrivateKeyPassphrase missing");

    if (!Path.IsPathRooted(keyPath)) keyPath = Path.GetFullPath(keyPath);
    if (!File.Exists(keyPath)) throw new FileNotFoundException($"Snowflake private key file not found: {keyPath}");

    var keyText = (await File.ReadAllTextAsync(keyPath)).ReplaceLineEndings("\n");

    var csb = new SnowflakeDbConnectionStringBuilder
    {
        ["ACCOUNT"] = account,
        ["HOST"] = $"{account}.snowflakecomputing.com",
        ["USER"] = user,
        ["AUTHENTICATOR"] = "snowflake_jwt",
        ["PRIVATE_KEY"] = keyText,
        ["PRIVATE_KEY_PWD"] = passphrase,
        ["WAREHOUSE"] = warehouse,
        ["ROLE"] = role
    };

    using var conn = new SnowflakeDbConnection { ConnectionString = csb.ConnectionString };
    await conn.OpenAsync();

    await SetSessionContextAsync(conn, role, warehouse, database, schema);

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
