using Barbershop.Data;
using Barbershop.Models.Domain;
using Barbershop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(ApplicationDbContext db, IAppointmentService appointmentService)
    {
        _db = db;
        _appointmentService = appointmentService;
    }

    public async Task<IActionResult> Index(string? status = null, string? date = null, int? barberId = null)
    {
        var query = _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.BookedServices)
                .ThenInclude(bs => bs.Service)
            .Include(a => a.Barber)
            .Include(a => a.Payment)
            .AsQueryable();

        if (Enum.TryParse<AppointmentStatus>(status, out var parsedStatus))
            query = query.Where(a => a.Status == parsedStatus);

        if (DateOnly.TryParse(date, out var parsedDate))
            query = query.Where(a => a.AppointmentDate == parsedDate);

        if (barberId.HasValue)
            query = query.Where(a => a.BarberId == barberId.Value);

        var appointments = await query
            .OrderByDescending(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .ToListAsync();

        ViewBag.Barbers = await _db.Barbers.Where(b => b.IsActive).OrderBy(b => b.LastName).ToListAsync();
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentDate = date;
        ViewBag.CurrentBarberId = barberId;

        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.BookedServices)
                .ThenInclude(bs => bs.Service)
            .Include(a => a.Barber)
            .Include(a => a.Payment)
            .Include(a => a.Review)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null) return NotFound();
        return View(appointment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        await _appointmentService.ConfirmAppointmentAsync(id);
        TempData["Success"] = "Запись подтверждена";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        await _appointmentService.CompleteAppointmentAsync(id);
        TempData["Success"] = "Запись завершена";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await _appointmentService.CancelAppointmentAsync(id, "", isAdmin: true, reason);
        TempData["Success"] = "Запись отменена";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int appointmentId, PaymentMethod method, decimal amount, string? notes)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Payment)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null) return NotFound();

        if (appointment.Payment == null)
        {
            _db.Payments.Add(new Payment
            {
                AppointmentId = appointmentId,
                Amount = amount,
                Method = method,
                Status = PaymentStatus.Paid,
                PaidAt = DateTime.UtcNow,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            appointment.Payment.Amount = amount;
            appointment.Payment.Method = method;
            appointment.Payment.Status = PaymentStatus.Paid;
            appointment.Payment.PaidAt = DateTime.UtcNow;
            appointment.Payment.Notes = notes;
        }

        if (appointment.Status != AppointmentStatus.Completed)
            await _appointmentService.CompleteAppointmentAsync(appointmentId);
        else
            await _db.SaveChangesAsync();

        TempData["Success"] = "Оплата зафиксирована";
        return RedirectToAction(nameof(Details), new { id = appointmentId });
    }
}
