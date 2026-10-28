using System.ComponentModel.DataAnnotations;
namespace EquipeEmPonto.Models;
public class Employee
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(8)] public string Code { get; set; } = "";
    [StringLength(32)] public string? RfidUid { get; set; }
    public bool Active { get; set; } = true;
    // UTC evita o erro do PostgreSQL com DateTimeOffset local (-03:00).
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<EmployeeTask> Tasks { get; set; } = [];
    public List<Punch> Punches { get; set; } = [];
}
