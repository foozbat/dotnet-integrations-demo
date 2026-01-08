using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IntegrationsDemo;

public class AzureSQLDbContext(DbContextOptions<AzureSQLDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        IEnumerable<EntityEntry<User>> entries = ChangeTracker.Entries<User>();
        DateTime now = DateTime.UtcNow;

        foreach (EntityEntry<User> entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
