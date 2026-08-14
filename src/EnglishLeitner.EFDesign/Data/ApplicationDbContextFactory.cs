using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnglishLeitner.EFDesign.Data;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Uses standard Microsoft.Data.Sqlite (NOT SqliteWasmConnection) because:
/// - No browser/worker required at design-time
/// - EF tools only need to inspect the model
/// - Runtime uses SqliteWasmConnection with OPFS
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Use standard SQLite for design-time (no WASM, no worker, no browser)
        optionsBuilder.UseSqlite("Data Source=:memory:");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
