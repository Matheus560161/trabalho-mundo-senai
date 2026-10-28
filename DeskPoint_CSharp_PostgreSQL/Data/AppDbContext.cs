using EquipeEmPonto.Models;
using Microsoft.EntityFrameworkCore;
namespace EquipeEmPonto.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeTask> Tasks => Set<EmployeeTask>();
    public DbSet<Punch> Punches => Set<Punch>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>().HasIndex(e => e.Code).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(e => e.RfidUid).IsUnique();
        modelBuilder.Entity<AdminUser>().HasIndex(a => a.Email).IsUnique();
        modelBuilder.Entity<EmployeeTask>().HasOne(t => t.Employee)
            .WithMany(e => e.Tasks).HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Punch>().HasOne(p => p.Employee)
            .WithMany(e => e.Punches).HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
