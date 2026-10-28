using System.Security.Claims;
using EquipeEmPonto.Data;
using EquipeEmPonto.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EquipeEmPonto.Controllers;
public class EmployeeController(AppDbContext db) : Controller
{
    [AllowAnonymous] public IActionResult Login() => View();
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken] public async Task<IActionResult> Login(string code) { var employee = await db.Employees.FirstOrDefaultAsync(e => e.Active && e.Code == (code ?? "").Trim().ToUpperInvariant()); if (employee == null) { ViewBag.Error = "Código não encontrado. Confira com o chefe."; return View(); } var claims = new[] { new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()), new Claim(ClaimTypes.Name, employee.Name), new Claim(ClaimTypes.Role, "Employee") }; await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme))); return RedirectToAction(nameof(Area)); }
    [Authorize(Roles="Employee")] public async Task<IActionResult> Area() { if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return RedirectToAction(nameof(Login)); ViewBag.Employee = await db.Employees.Include(e => e.Tasks).FirstOrDefaultAsync(e => e.Id == id && e.Active); ViewBag.Punches = await db.Punches.Where(p => p.EmployeeId == id).OrderByDescending(p => p.PunchedAt).Take(30).ToListAsync(); if (ViewBag.Employee == null) return RedirectToAction(nameof(Login)); return View(); }
    [HttpPost, Authorize(Roles="Employee"), ValidateAntiForgeryToken] public async Task<IActionResult> ToggleTask(int id) { if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)) return RedirectToAction(nameof(Login)); var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId); if (task != null) { task.Done = !task.Done; await db.SaveChangesAsync(); } return RedirectToAction(nameof(Area)); }
    [HttpPost, Authorize(Roles="Employee"), ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction("Index", "Home"); }
}
