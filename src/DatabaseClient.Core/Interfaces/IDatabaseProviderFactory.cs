using DatabaseClient.Core.Models;

namespace DatabaseClient.Core.Interfaces;

/// <summary>
/// Factory that returns the correct <see cref="IDatabaseProvider"/> for a given database type.
/// </summary>
public interface IDatabaseProviderFactory
{
    IDatabaseProvider Create(DatabaseType databaseType);
}
