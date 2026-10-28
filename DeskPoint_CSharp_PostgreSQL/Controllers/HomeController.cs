using EquipeEmPonto.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EquipeEmPonto.Controllers;
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.RecentPunch = await db.Punches.Include(p => p.Employee).OrderByDescending(p => p.PunchedAt).FirstOrDefaultAsync(); ViewBag.ActiveCount = await db.Employees.CountAsync(e => e.Active); return View(); }
    [Route("Home/Error")] public IActionResult Error() => View();
}
