using Barbershop.Data;
using Barbershop.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PaymentsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? status = null, string? dateFrom = null, string? dateTo = null)
    {
        var query = _db.Payments
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Client)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.BookedServices)
                    .ThenInclude(bs => bs.Service)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Barber)
            .AsQueryable();

        if (Enum.TryParse<PaymentStatus>(status, out var parsedStatus))
            query = query.Where(p => p.Status == parsedStatus);

        if (DateTime.TryParse(dateFrom, out var from))
            query = query.Where(p => p.CreatedAt >= from);

        if (DateTime.TryParse(dateTo, out var to))
            query = query.Where(p => p.CreatedAt <= to.AddDays(1));

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.TotalPaid = payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount);
        ViewBag.CurrentStatus = status;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;

        return View(payments);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Refund(int id, string? notes)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment == null) return NotFound();

        payment.Status = PaymentStatus.Refunded;
        payment.Notes = notes;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Возврат оформлен";
        return RedirectToAction(nameof(Index));
    }
}
