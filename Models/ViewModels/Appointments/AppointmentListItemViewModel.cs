using Barbershop.Models.Domain;

namespace Barbershop.Models.ViewModels.Appointments;

public class AppointmentListItemViewModel
{
    public int Id { get; set; }
    public List<string> ServiceNames { get; set; } = [];
    public string BarberName { get; set; } = string.Empty;
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal TotalPrice { get; set; }
    public AppointmentStatus Status { get; set; }
    public bool CanCancel => Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
    public bool CanReview => Status == AppointmentStatus.Completed;
}
