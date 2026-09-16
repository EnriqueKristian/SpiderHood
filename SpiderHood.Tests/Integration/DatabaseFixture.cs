using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpiderHood.Data;

namespace SpiderHood.Tests.Integration;

// Lee la misma connection string que usa la app en desarrollo
// (SpiderHood/appsettings.Development.json, ignorado por git -- ver README de
// desarrollo) para correr los tests de integración contra un SQL Server real.
// Si ese archivo no existe o el servidor no responde, los tests se saltan en
// vez de fallar: no todos los entornos (CI, un checkout nuevo) tienen un SQL
// Server local configurado.
public sealed class DatabaseFixture
{
    public bool IsAvailable { get; }
    public string? SkipReason { get; }

    private readonly IDbContextFactory<SpiderHoodContext>? _contextFactory;

    // Cachea, la primera vez que se pide, el IdBuilding de un edificio activo real de la
    // base restaurada -- así los tests que necesitan un Guid de edificio no lo hardcodean
    // (cualquier instalación/backup que se restaure sirve) ni disparan una consulta nueva
    // cada vez. Lazy<T> es thread-safe por default: si dos tests lo piden a la vez, sólo
    // una llamada real corre y las demás esperan la misma Task.
    private readonly Lazy<Task<Guid>>? _anyBuildingId;

    public DatabaseFixture()
    {
        var connectionString = TryReadConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            SkipReason = "No se encontró SpiderHood/appsettings.Development.json con " +
                         "ConnectionStrings:SpiderHoodContext -- estos tests de integración " +
                         "necesitan un SQL Server local (ver README > Development Setup).";
            return;
        }

        try
        {
            var options = new DbContextOptionsBuilder<SpiderHoodContext>()
                .UseSqlServer(connectionString)
                .Options;

            var factory = new PooledDbContextFactory<SpiderHoodContext>(options);

            using var probe = factory.CreateDbContext();
            probe.Database.OpenConnection();
            probe.Database.CloseConnection();

            _contextFactory = factory;
            IsAvailable = true;
            _anyBuildingId = new Lazy<Task<Guid>>(FetchAnyBuildingIdAsync);
        }
        catch (Exception ex)
        {
            SkipReason = $"No se pudo conectar a SQL Server ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    public BDLayout CreateBDLayout()
    {
        if (_contextFactory is null)
            throw new InvalidOperationException("La base de datos de pruebas no está disponible -- revisar IsAvailable/SkipReason antes de llamar esto.");

        return new BDLayout(_contextFactory);
    }

    // Para tests de módulos "por edificio" (Owners, Categorías, Presupuestos, etc.) que
    // necesitan CUALQUIER edificio real, no uno específico -- evita tener que sembrar
    // datos propios o conocer de antemano el Guid de un edificio del backup restaurado.
    public Task<Guid> GetAnyBuildingIdAsync() =>
        _anyBuildingId?.Value ?? throw new InvalidOperationException("La base de datos de pruebas no está disponible.");

    private async Task<Guid> FetchAnyBuildingIdAsync()
    {
        var buildings = await CreateBDLayout().GetAllBuildingsPublicAsync();
        if (buildings.Count == 0)
            throw new InvalidOperationException("No hay edificios activos en la base de datos de pruebas -- GetAnyBuildingIdAsync necesita al menos uno.");

        return buildings[0].IdBuilding;
    }

    private static string? TryReadConnectionString()
    {
        var path = FindAppSettingsDevelopmentPath();
        if (path is null)
            return null;

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
               connectionStrings.TryGetProperty("SpiderHoodContext", out var connectionString)
            ? connectionString.GetString()
            : null;
    }

    // Sube desde bin/Debug/net10.0 hasta encontrar SpiderHood.slnx (raíz del repo),
    // para no depender de dónde corre el test runner.
    private static string? FindAppSettingsDevelopmentPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SpiderHood.slnx")))
            {
                var candidate = Path.Combine(dir.FullName, "SpiderHood", "appsettings.Development.json");
                return File.Exists(candidate) ? candidate : null;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
