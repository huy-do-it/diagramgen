using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace UMLForge.BuildingBlocks.Tests.Infrastructure;

/// <summary>Redis 7 + SQL Server 2022 thật trong Docker, khởi động 1 lần cho cả collection.</summary>
public sealed class ContainersFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string RedisConnectionString => _redis.GetConnectionString();

    /// <summary>Mỗi test dùng 1 database riêng để không ảnh hưởng nhau.</summary>
    public string SqlConnectionString(string database)
        => _sql.GetConnectionString().Replace("Database=master", $"Database={database}", StringComparison.OrdinalIgnoreCase);

    public Task InitializeAsync() => Task.WhenAll(_redis.StartAsync(), _sql.StartAsync());

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ContainersCollection : ICollectionFixture<ContainersFixture>
{
    public const string Name = "containers";
}
