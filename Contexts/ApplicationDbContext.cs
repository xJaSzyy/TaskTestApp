using Microsoft.EntityFrameworkCore;
using TaskTestApp.Models;

namespace TaskTestApp.Contexts;

public class ApplicationDbContext : DbContext
{
    public virtual DbSet<TaskItem> TaskItems { get; set; } = null!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}