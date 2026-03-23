using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IPM.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=IPMProDB;User Id=sa;Password=Dung@28112005;TrustServerCertificate=True;MultipleActiveResultSets=true");

        var databaseSettings = new DatabaseSettings
        {
            TablePrefix = "123"
        };

        return new ApplicationDbContext(optionsBuilder.Options, databaseSettings);
    }
}