using Barbershop.Models.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Barbershop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServiceCategory> ServiceCategories { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Barber> Barbers { get; set; }
    public DbSet<BarberService> BarberServices { get; set; }
    public DbSet<WorkDay> WorkDays { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<BookedService> BookedServices { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Схема: identity ──────────────────────────────────────────────────
        builder.Entity<ApplicationUser>().ToTable("Users", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims", "identity");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens", "identity");

        // ── Схема: services ──────────────────────────────────────────────────
        builder.Entity<ServiceCategory>().ToTable("ServiceCategories", "services");
        builder.Entity<Service>().ToTable("Services", "services");
        builder.Entity<BarberService>().ToTable("BarberServices", "services");

        // ── Схема: appointments ──────────────────────────────────────────────
        builder.Entity<Appointment>().ToTable("Appointments", "appointments");
        builder.Entity<BookedService>().ToTable("BookedServices", "appointments");
        builder.Entity<Payment>().ToTable("Payments", "appointments");
        builder.Entity<Review>().ToTable("Reviews", "appointments");

        // ── Схема: staff ─────────────────────────────────────────────────────
        builder.Entity<Barber>().ToTable("Barbers", "staff");
        builder.Entity<WorkDay>().ToTable("WorkDays", "staff");
        builder.Entity<Schedule>().ToTable("Schedules", "staff");

        // ── Схема: audit ─────────────────────────────────────────────────────
        builder.Entity<AuditLog>().ToTable("AuditLogs", "audit");
        builder.Entity<Notification>().ToTable("Notifications", "audit");

        // BarberService: составной PK (Many-to-Many)
        builder.Entity<BarberService>()
            .HasKey(bs => new { bs.BarberId, bs.ServiceId });

        builder.Entity<BarberService>()
            .HasOne(bs => bs.Barber)
            .WithMany(b => b.BarberServices)
            .HasForeignKey(bs => bs.BarberId);

        builder.Entity<BarberService>()
            .HasOne(bs => bs.Service)
            .WithMany(s => s.BarberServices)
            .HasForeignKey(bs => bs.ServiceId);

        // BookedService: составной PK (Many-to-Many с данными)
        builder.Entity<BookedService>()
            .HasKey(bs => new { bs.AppointmentId, bs.ServiceId });

        builder.Entity<BookedService>()
            .HasOne(bs => bs.Appointment)
            .WithMany(a => a.BookedServices)
            .HasForeignKey(bs => bs.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BookedService>()
            .HasOne(bs => bs.Service)
            .WithMany(s => s.BookedServices)
            .HasForeignKey(bs => bs.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BookedService>()
            .Property(bs => bs.PriceAtTime)
            .HasPrecision(10, 2);

        // Appointment -> Client (нельзя каскадно удалить пользователя с записями)
        builder.Entity<Appointment>()
            .HasOne(a => a.Client)
            .WithMany(u => u.Appointments)
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review -> Client
        builder.Entity<Review>()
            .HasOne(r => r.Client)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review -> Barber
        builder.Entity<Review>()
            .HasOne(r => r.Barber)
            .WithMany(b => b.Reviews)
            .HasForeignKey(r => r.BarberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review -> Appointment (один к одному)
        builder.Entity<Review>()
            .HasOne(r => r.Appointment)
            .WithOne(a => a.Review)
            .HasForeignKey<Review>(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Payment -> Appointment (один к одному)
        builder.Entity<Payment>()
            .HasOne(p => p.Appointment)
            .WithOne(a => a.Payment)
            .HasForeignKey<Payment>(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Точность decimal
        builder.Entity<Service>()
            .Property(s => s.Price)
            .HasPrecision(10, 2);

        builder.Entity<Appointment>()
            .Property(a => a.TotalPrice)
            .HasPrecision(10, 2);

        builder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(10, 2);

        // Индексы
        builder.Entity<Appointment>()
            .HasIndex(a => new { a.BarberId, a.AppointmentDate, a.StartTime });

        builder.Entity<Appointment>()
            .HasIndex(a => new { a.ClientId, a.AppointmentDate });

        builder.Entity<Schedule>()
            .HasIndex(s => new { s.BarberId, s.Date })
            .IsUnique();

        builder.Entity<WorkDay>()
            .HasIndex(w => new { w.BarberId, w.DayOfWeek })
            .IsUnique();
    }
}
