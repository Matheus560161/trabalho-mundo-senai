namespace EquipeEmPonto.Models;
public class Punch
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTimeOffset PunchedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Kind { get; set; } = "entrada";
}
