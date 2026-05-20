using Barbershop.Data;
using Barbershop.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Services;

public class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _db;

    public AppointmentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<TimeOnly>> GetAvailableSlotsAsync(int barberId, DateOnly date, List<int> serviceIds)
    {
        if (serviceIds.Count == 0) return [];

        var services = await _db.Services
            .Where(s => serviceIds.Contains(s.Id))
            .ToListAsync();

        if (services.Count != serviceIds.Count) return [];

        var totalDuration = services.Sum(s => s.Duration);

        // Проверяем переопределение расписания на конкретную дату
        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.BarberId == barberId && s.Date == date);

        TimeOnly workStart, workEnd;
        int slotDuration;

        if (schedule != null)
        {
            if (!schedule.IsAvailable) return [];
            workStart = schedule.StartTime;
            workEnd = schedule.EndTime;
            slotDuration = 30;
        }
        else
        {
            var workDay = await _db.WorkDays
                .FirstOrDefaultAsync(w => w.BarberId == barberId && w.DayOfWeek == date.DayOfWeek);

            if (workDay == null || !workDay.IsWorking) return [];
            workStart = workDay.StartTime;
            workEnd = workDay.EndTime;
            slotDuration = workDay.SlotDurationMinutes;
        }

        // Загружаем уже занятые слоты
        var existingAppointments = await _db.Appointments
            .Where(a => a.BarberId == barberId
                        && a.AppointmentDate == date
                        && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var slots = new List<TimeOnly>();
        var current = workStart;
        var serviceDurationSpan = TimeSpan.FromMinutes(totalDuration);

        while (current.Add(serviceDurationSpan) <= workEnd)
        {
            var slotEnd = current.Add(serviceDurationSpan);
            var isOccupied = existingAppointments.Any(a =>
                current < a.EndTime && slotEnd > a.StartTime);

            if (!isOccupied)
                slots.Add(current);

            current = current.Add(TimeSpan.FromMinutes(slotDuration));
        }

        return slots;
    }

    public async Task<bool> IsSlotAvailableAsync(int barberId, DateOnly date, TimeOnly startTime, TimeOnly endTime, int? excludeId = null)
    {
        var query = _db.Appointments
            .Where(a => a.BarberId == barberId
                        && a.AppointmentDate == date
                        && a.Status != AppointmentStatus.Cancelled
                        && a.StartTime < endTime
                        && a.EndTime > startTime);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<Appointment> CreateAppointmentAsync(string clientId, int barberId, List<int> serviceIds, DateOnly date, TimeOnly startTime, string? notes)
    {
        if (serviceIds.Count == 0)
            throw new InvalidOperationException("Выберите хотя бы одну услугу");

        var services = await _db.Services
            .Where(s => serviceIds.Contains(s.Id) && s.IsActive)
            .ToListAsync();

        if (services.Count != serviceIds.Count)
            throw new InvalidOperationException("Одна или несколько услуг не найдены");

        var totalDuration = services.Sum(s => s.Duration);
        var totalPrice = services.Sum(s => s.Price);
        var endTime = startTime.Add(TimeSpan.FromMinutes(totalDuration));

        if (!await IsSlotAvailableAsync(barberId, date, startTime, endTime))
            throw new InvalidOperationException("Выбранное время уже занято");

        var appointment = new Appointment
        {
            ClientId = clientId,
            BarberId = barberId,
            AppointmentDate = date,
            StartTime = startTime,
            EndTime = endTime,
            TotalPrice = totalPrice,
            Notes = notes,
            Status = AppointmentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var service in services)
        {
            appointment.BookedServices.Add(new BookedService
            {
                ServiceId = service.Id,
                PriceAtTime = service.Price,
                DurationAtTime = service.Duration
            });
        }

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return appointment;
    }

    public async Task<bool> CancelAppointmentAsync(int appointmentId, string userId, bool isAdmin, string? reason)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Payment)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null) return false;

        if (!isAdmin && appointment.ClientId != userId) return false;

        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            return false;

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancelReason = reason;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Если оплата уже была зафиксирована — автоматически ставим возврат
        if (appointment.Payment?.Status == PaymentStatus.Paid)
        {
            appointment.Payment.Status = PaymentStatus.Refunded;
            appointment.Payment.Notes = string.IsNullOrWhiteSpace(reason)
                ? "Автовозврат при отмене записи"
                : $"Автовозврат при отмене записи: {reason}";
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ConfirmAppointmentAsync(int appointmentId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId);
        if (appointment == null || appointment.Status != AppointmentStatus.Pending) return false;

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteAppointmentAsync(int appointmentId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId);
        if (appointment == null || appointment.Status == AppointmentStatus.Cancelled) return false;

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

