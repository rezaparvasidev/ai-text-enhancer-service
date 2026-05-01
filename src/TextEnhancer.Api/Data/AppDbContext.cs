using Microsoft.EntityFrameworkCore;

namespace TextEnhancer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Interaction> Interactions => Set<Interaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var interaction = modelBuilder.Entity<Interaction>();
        interaction.HasKey(i => i.Id);
        interaction.Property(i => i.InputText).IsRequired();
        interaction.Property(i => i.Model).HasMaxLength(64);
        interaction.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
        interaction.HasIndex(i => i.CreatedUtc);
    }
}
