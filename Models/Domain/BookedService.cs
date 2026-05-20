namespace Barbershop.Models.Domain;

public class BookedService
{
    public int AppointmentId { get; set; }
    public int ServiceId { get; set; }
    public decimal PriceAtTime { get; set; }
    public int DurationAtTime { get; set; } // минуты на момент записи

    public Appointment Appointment { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
