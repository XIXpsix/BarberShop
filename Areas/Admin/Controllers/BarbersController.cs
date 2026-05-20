using Barbershop.Data;
using Barbershop.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Adminя,Manager")]
public class BarbersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public BarbersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var barbers = await _db.Barbers
            .Include(b => b.BarberServices)
            .ThenInclude(bs => bs.Service)
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.LastName)
            .ToListAsync();
        return View(barbers);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateServices();
        return View(new Barber());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Barber model, int[] selectedServices)
    {
        if (!ModelState.IsValid)
        {
            await PopulateServices(selectedServices);
            return View(model);
        }
        model.CreatedAt = DateTime.UtcNow;
        _db.Barbers.Add(model);
        await _db.SaveChangesAsync();

        foreach (var sId in selectedServices)
            _db.BarberServices.Add(new BarberService { BarberId = model.Id, ServiceId = sId });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Мастер добавлен";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var barber = await _db.Barbers
            .Include(b => b.BarberServices)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (barber == null) return NotFound();
        var selected = barber.BarberServices.Select(bs => bs.ServiceId).ToArray();
        await PopulateServices(selected);
        return View(barber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Barber model, int[] selectedServices)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateServices(selectedServices);
            return View(model);
        }
        _db.Barbers.Update(model);

        var existing = await _db.BarberServices.Where(bs => bs.BarberId == id).ToListAsync();
        _db.BarberServices.RemoveRange(existing);
        foreach (var sId in selectedServices)
            _db.BarberServices.Add(new BarberService { BarberId = id, ServiceId = sId });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Мастер обновлён";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Schedule(int id)
    {
        var barber = await _db.Barbers
            .Include(b => b.WorkDays)
            .Include(b => b.Schedules)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (barber == null) return NotFound();
        ViewBag.Barber = barber;
        return View(barber);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWorkDays(int barberId, List<WorkDay> workDays)
    {
        var existing = await _db.WorkDays.Where(w => w.BarberId == barberId).ToListAsync();
        _db.WorkDays.RemoveRange(existing);
        foreach (var wd in workDays)
        {
            wd.BarberId = barberId;
            _db.WorkDays.Add(wd);
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Расписание сохранено";
        return RedirectToAction(nameof(Schedule), new { id = barberId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScheduleOverride(Schedule model)
    {
        var existing = await _db.Schedules
            .FirstOrDefaultAsync(s => s.BarberId == model.BarberId && s.Date == model.Date);
        if (existing != null)
            _db.Schedules.Remove(existing);

        _db.Schedules.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Особый день добавлен";
        return RedirectToAction(nameof(Schedule), new { id = model.BarberId });
    }

    private async Task PopulateServices(int[]? selected = null)
    {
        var services = await _db.Services.Where(s => s.IsActive).Include(s => s.Category).OrderBy(s => s.Name).ToListAsync();
        ViewBag.AllServices = services;
        ViewBag.SelectedServices = selected ?? [];
    }
}
