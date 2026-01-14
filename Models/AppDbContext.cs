using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text;

namespace PartTracker.Models;

public class AppDbContext : DbContext
{
	
	public DbSet<PartsInTransit> PartsInTransit { get; set; }
	public DbSet<ChangeLogEntry> ChangeLogEntries { get; set; }
	public DbSet<ActivityForm> ActivityForms { get; set; }
	public DbSet<SafetyStockItem> SafetyStockItems { get; set; }

	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}
	private static string ToSnakeCase(string input)
	{
		if (string.IsNullOrEmpty(input)) return input;

		var builder = new StringBuilder();
		for (int i = 0; i < input.Length; i++)
		{
			var c = input[i];
			if (char.IsUpper(c))
			{
				if (i > 0) builder.Append('_');
				builder.Append(char.ToLowerInvariant(c));
			}
			else
			{
				builder.Append(c);
			}
		}
		return builder.ToString();
	}

	//converts the c# naming convention to the MySql Naming convention
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

		foreach (var entity in modelBuilder.Model.GetEntityTypes())
		{
			var tableName = entity.GetTableName();
			if (!string.IsNullOrEmpty(tableName))
			{
				entity.SetTableName(ToSnakeCase(tableName));
			}

			foreach (var property in entity.GetProperties())
			{
				if (!string.IsNullOrEmpty(property.Name))
				{
					property.SetColumnName(ToSnakeCase(property.Name));
				}
			}

			foreach (var key in entity.GetKeys())
			{
				var keyName = key.GetName();
				if (!string.IsNullOrEmpty(keyName))
				{
					key.SetName(ToSnakeCase(keyName));
				}
			}

			foreach (var foreignKey in entity.GetForeignKeys())
			{
				var constraintName = foreignKey.GetConstraintName();
				if (!string.IsNullOrEmpty(constraintName))
				{
					foreignKey.SetConstraintName(ToSnakeCase(constraintName));
				}
			}

			foreach (var index in entity.GetIndexes())
			{
				var indexName = index.GetDatabaseName();
				if (!string.IsNullOrEmpty(indexName))
				{
					index.SetDatabaseName(ToSnakeCase(indexName));
				}
			}
		}
	}
}
