using Barbershop.Models.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Barbershop.Models.ViewModels.Appointments;

public class BookAppointmentViewModel
{
    [Required(ErrorMessage = "Выберите хотя бы одну услугу")]
    [Display(Name = "Услуги")]
    [MinLength(1, ErrorMessage = "Выберите хотя бы одну услугу")]
    public List<int> ServiceIds { get; set; } = [];

    [Required(ErrorMessage = "Выберите мастера")]
    [Display(Name = "Мастер")]
    public int BarberId { get; set; }

    [Required(ErrorMessage = "Выберите дату")]
    [Display(Name = "Дата")]
    public DateOnly AppointmentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    [Required(ErrorMessage = "Выберите время")]
    [Display(Name = "Время")]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Комментарий")]
    [StringLength(500)]
    public string? Notes { get; set; }

    // Для формы
    public IEnumerable<SelectListItem> Services { get; set; } = [];
    public IEnumerable<SelectListItem> Barbers { get; set; } = [];
    public List<TimeOnly> AvailableSlots { get; set; } = [];
}
