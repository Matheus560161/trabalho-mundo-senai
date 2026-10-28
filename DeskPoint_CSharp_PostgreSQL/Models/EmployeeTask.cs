using System.ComponentModel.DataAnnotations;
namespace EquipeEmPonto.Models;
public class EmployeeTask
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [Required, StringLength(250)] public string Title { get; set; } = "";
    public bool Done { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
