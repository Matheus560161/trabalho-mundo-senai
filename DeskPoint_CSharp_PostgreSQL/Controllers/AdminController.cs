using System.Security.Claims;
using System.Security.Cryptography;
using EquipeEmPonto.Data;
using EquipeEmPonto.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EquipeEmPonto.Controllers;
public class AdminController(AppDbContext db) : Controller
{
    [AllowAnonymous] public IActionResult Login() => View();
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken] public async Task<IActionResult> Login(string email, string password) {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == (email ?? "").Trim().ToLowerInvariant());
        if (admin is null || !VerifyPassword(password ?? "", admin.PasswordHash)) { ViewBag.Error = "E-mail ou senha incorretos."; return View(); }
        var claims = new[] { new Claim(ClaimTypes.Name, admin.Email), new Claim(ClaimTypes.Role, "Admin") };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToAction(nameof(Index));
    }
    [Authorize(Roles="Admin")] public async Task<IActionResult> Index() {
        ViewBag.Employees = await db.Employees.Include(e => e.Tasks).Where(e => e.Active).OrderBy(e => e.Name).ToListAsync();
        ViewBag.Punches = await db.Punches.Include(p => p.Employee).OrderByDescending(p => p.PunchedAt).Take(100).ToListAsync();
        ViewBag.TotalEmployees = await db.Employees.CountAsync(e => e.Active);
        ViewBag.TotalTasks = await db.Tasks.CountAsync(t => !t.Done);
        ViewBag.TodayPunches = await db.Punches.CountAsync(p => p.PunchedAt >= DateTimeOffset.UtcNow.Date);
        return View();
    }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> AddEmployee(string name, string? rfidUid) {
        if (string.IsNullOrWhiteSpace(name)) TempData["Message"] = "Informe o nome do funcionário.";
        else if (await db.Employees.CountAsync(e => e.Active) >= 5) TempData["Message"] = "O limite de 5 funcionários ativos foi atingido.";
        else { var uid = string.IsNullOrWhiteSpace(rfidUid) ? null : rfidUid.Trim().Replace(" ", "").ToUpperInvariant();
            if (uid != null && await db.Employees.AnyAsync(e => e.RfidUid == uid)) TempData["Message"] = "Esse cartão já está cadastrado.";
            else { db.Employees.Add(new Employee { Name = name.Trim(), Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), RfidUid = uid, CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); TempData["Message"] = "Funcionário cadastrado com sucesso."; } }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> AddTask(int employeeId, string title) {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.Active);
        if (employee == null) TempData["Message"] = "Funcionário não encontrado ou inativo.";
        else if (string.IsNullOrWhiteSpace(title)) TempData["Message"] = "Digite o nome da tarefa.";
        else { db.Tasks.Add(new EmployeeTask { EmployeeId = employeeId, Title = title.Trim(), CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); TempData["Message"] = "Tarefa adicionada."; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> ToggleTask(int id) { var task = await db.Tasks.FindAsync(id); if (task != null) { task.Done = !task.Done; await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> DeleteTask(int id) { var task = await db.Tasks.FindAsync(id); if (task != null) { db.Tasks.Remove(task); await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> Fire(int id) { var employee = await db.Employees.FindAsync(id); if (employee != null) { employee.Active = false; await db.SaveChangesAsync(); TempData["Message"] = "Funcionário desativado. O histórico foi preservado."; } return RedirectToAction(nameof(Index)); }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> UpdateRfid(int id, string? rfidUid) { var employee = await db.Employees.FindAsync(id); if (employee == null) TempData["Message"] = "Funcionário não encontrado."; else { var uid = string.IsNullOrWhiteSpace(rfidUid) ? null : rfidUid.Trim().Replace(" ", "").ToUpperInvariant(); if (uid != null && await db.Employees.AnyAsync(e => e.Id != id && e.RfidUid == uid)) TempData["Message"] = "Esse cartão já está associado a outra pessoa."; else { employee.RfidUid = uid; await db.SaveChangesAsync(); TempData["Message"] = "Cartão atualizado."; } } return RedirectToAction(nameof(Index)); }
    [HttpPost, Authorize(Roles="Admin"), ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction("Index", "Home"); }
    private static bool VerifyPassword(string password, string stored) { var parts = stored.Split('.'); if (parts.Length != 2) return false; try { var salt = Convert.FromBase64String(parts[0]); var expected = Convert.FromBase64String(parts[1]); var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length); return CryptographicOperations.FixedTimeEquals(actual, expected); } catch { return false; } }
}
