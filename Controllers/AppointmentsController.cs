using Barbershop.Data;
using Barbershop.Models.Domain;
using Barbershop.Models.ViewModels.Appointments;
using Barbershop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Controllers;

[Authorize]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAppointmentService _appointmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppointmentsController(ApplicationDbContext db,
        IAppointmentService appointmentService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _appointmentService = appointmentService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Book(int? serviceId = null, int? barberId = null)
    {
        var services = await _db.Services
            .Where(s => s.IsActive)
            .Include(s => s.Category)
            .OrderBy(s => s.Category.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        var barbers = await _db.Barbers
            .Where(b => b.IsActive)
            .ToListAsync();

        var model = new BookAppointmentViewModel
        {
            ServiceIds = serviceId.HasValue ? [serviceId.Value] : [],
            BarberId = barberId ?? 0,
            Services = services.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{s.Category.Name} — {s.Name} ({s.Duration} мин, {s.Price:N0} ₽)"
            }),
            Barbers = barbers.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.FullName} (стаж {b.ExperienceYears} лет)"
            })
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBookViewModel(model);
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var appointment = await _appointmentService.CreateAppointmentAsync(
                user.Id, model.BarberId, model.ServiceIds,
                model.AppointmentDate, model.StartTime, model.Notes);

            TempData["Success"] = "Запись успешно создана! Ожидайте подтверждения.";
            return RedirectToAction("Confirmation", new { id = appointment.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await PopulateBookViewModel(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var appointment = await _db.Appointments
            .Include(a => a.BookedServices)
                .ThenInclude(bs => bs.Service)
            .Include(a => a.Barber)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == user!.Id);

        if (appointment == null) return NotFound();
        return View(appointment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var success = await _appointmentService.CancelAppointmentAsync(id, user.Id, false, reason);
        TempData[success ? "Success" : "Error"] = success
            ? "Запись отменена"
            : "Не удалось отменить запись";

        return RedirectToAction("MyAppointments", "Account");
    }

    // AJAX: получить свободные слоты
    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots(int barberId, string date, [FromQuery] List<int> serviceIds)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest();

        var slots = await _appointmentService.GetAvailableSlotsAsync(barberId, parsedDate, serviceIds);
        return Json(slots.Select(s => s.ToString("HH:mm")));
    }

    private async Task PopulateBookViewModel(BookAppointmentViewModel model)
    {
        var services = await _db.Services.Where(s => s.IsActive).Include(s => s.Category).OrderBy(s => s.Name).ToListAsync();
        var barbers = await _db.Barbers.Where(b => b.IsActive).ToListAsync();
        model.Services = services.Select(s => new SelectListItem
        {
            Value = s.Id.ToString(),
            Text = $"{s.Category.Name} — {s.Name} ({s.Duration} мин, {s.Price:N0} ₽)"
        });
        model.Barbers = barbers.Select(b => new SelectListItem
        {
            Value = b.Id.ToString(),
            Text = $"{b.FullName} (стаж {b.ExperienceYears} лет)"
        });
    }
}
