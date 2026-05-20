using Barbershop.Data;
using Barbershop.Models.Domain;
using Barbershop.Models.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        var rawAppointments = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.BookedServices)
                .ThenInclude(bs => bs.Service)
            .Include(a => a.Barber)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();

        var recentItems = rawAppointments.Select(a => new RecentAppointmentItem
        {
            Id = a.Id,
            ClientName = a.Client.FirstName + " " + a.Client.LastName,
            ServiceName = string.Join(", ", a.BookedServices.Select(bs => bs.Service.Name)),
            BarberName = a.Barber.FirstName + " " + a.Barber.LastName,
            Date = a.AppointmentDate,
            TimeRange = a.StartTime.ToString("HH:mm") + " – " + a.EndTime.ToString("HH:mm"),
            Status = a.Status.ToString(),
            StatusClass = a.Status switch
            {
                AppointmentStatus.Pending   => "warning",
                AppointmentStatus.Confirmed => "info",
                AppointmentStatus.Completed => "success",
                AppointmentStatus.Cancelled => "danger",
                _ => "secondary"
            }
        }).ToList();

        var vm = new DashboardViewModel
        {
            TotalAppointmentsToday = await _db.Appointments
                .CountAsync(a => a.AppointmentDate == today && a.Status != AppointmentStatus.Cancelled),

            PendingAppointments = await _db.Appointments
                .CountAsync(a => a.Status == AppointmentStatus.Pending),

            ConfirmedAppointments = await _db.Appointments
                .CountAsync(a => a.Status == AppointmentStatus.Confirmed),

            TotalClientsCount = await _db.Users
                .CountAsync(u => u.IsActive),

            ActiveBarbersCount = await _db.Barbers
                .CountAsync(b => b.IsActive),

            RevenueToday = await _db.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue
                    && DateOnly.FromDateTime(p.PaidAt.Value) == today)
                .SumAsync(p => (decimal?)p.Amount) ?? 0,

            RevenueThisMonth = await _db.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt.HasValue
                    && DateOnly.FromDateTime(p.PaidAt.Value) >= monthStart)
                .SumAsync(p => (decimal?)p.Amount) ?? 0,

            RecentAppointments = recentItems,

            BarberStats = await _db.Barbers
                .Where(b => b.IsActive)
                .Select(b => new BarberStatItem
                {
                    BarberName = b.FirstName + " " + b.LastName,
                    AppointmentsThisMonth = b.Appointments
                        .Count(a => a.AppointmentDate >= monthStart && a.Status != AppointmentStatus.Cancelled),
                    RevenueThisMonth = b.Appointments
                        .Where(a => a.AppointmentDate >= monthStart && a.Status == AppointmentStatus.Completed)
                        .Sum(a => (decimal?)a.TotalPrice) ?? 0
                })
                .ToListAsync()
        };

        return View(vm);
    }
}
