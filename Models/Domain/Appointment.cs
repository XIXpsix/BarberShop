namespace Barbershop.Models.Domain;

public class Appointment
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public int BarberId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ApplicationUser Client { get; set; } = null!;
    public Barber Barber { get; set; } = null!;
    public ICollection<BookedService> BookedServices { get; set; } = new List<BookedService>();
    public Payment? Payment { get; set; }
    public Review? Review { get; set; }
}
