namespace Barbershop.Models.Domain;

public class Service
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; } // minutes
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ServiceCategory Category { get; set; } = null!;
    public ICollection<BarberService> BarberServices { get; set; } = new List<BarberService>();
    public ICollection<BookedService> BookedServices { get; set; } = new List<BookedService>();
}
