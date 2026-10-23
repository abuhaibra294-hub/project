using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DareCare.Infrastructure.Persistence;

public sealed class DareCareDbContext(DbContextOptions<DareCareDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PatientProfile> Patients => Set<PatientProfile>();
    public DbSet<PharmacyProfile> Pharmacies => Set<PharmacyProfile>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<NurseProfile> Nurses => Set<NurseProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<NurseSkill> NurseSkills => Set<NurseSkill>();
    public DbSet<CareService> CareServices => Set<CareService>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<NurseReview> NurseReviews => Set<NurseReview>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PharmacyOrder> PharmacyOrders => Set<PharmacyOrder>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<PatientAddress> PatientAddresses => Set<PatientAddress>();
    public DbSet<FavoriteNurse> FavoriteNurses => Set<FavoriteNurse>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.TokenVersion).HasDefaultValue(0);
        });
        modelBuilder.Entity<PatientProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.DefaultAddress).HasMaxLength(400);
            entity.Property(x => x.EmergencyContactName).HasMaxLength(160);
            entity.Property(x => x.EmergencyContactPhone).HasMaxLength(32);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<PatientProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PharmacyProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.BusinessName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.LicenseNumber).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.LicenseNumber).IsUnique();
            entity.Property(x => x.City).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(400).IsRequired();
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<PharmacyProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasIndex(x => x.FamilyId);
            entity.Property(x => x.ReplacedByHash).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NurseProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.LicenseNumber).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.LicenseNumber).IsUnique();
            entity.Property(x => x.Bio).HasMaxLength(1500);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.ServiceArea).HasMaxLength(250);
            entity.Property(x => x.HourlyRate).HasPrecision(18, 2);
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<NurseProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<NurseSkill>(entity =>
        {
            entity.HasKey(x => new { x.NurseProfileId, x.SkillId });
            entity.HasOne(x => x.Nurse).WithMany(x => x.NurseSkills).HasForeignKey(x => x.NurseProfileId);
            entity.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CareService>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(800).IsRequired();
            entity.Property(x => x.BasePrice).HasPrecision(18, 2);
            entity.HasOne(x => x.RequiredSkill).WithMany().HasForeignKey(x => x.RequiredSkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.NurseProfileId, x.StartsAtUtc });
            entity.HasOne(x => x.Nurse).WithMany(x => x.AvailabilitySlots)
                .HasForeignKey(x => x.NurseProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Slot_ValidTime", "[EndsAtUtc] > [StartsAtUtc]"));
        });
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ServiceAddress).HasMaxLength(400).IsRequired();
            entity.Property(x => x.PriceAtBooking).HasPrecision(18, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            if (Database.IsSqlServer()) entity.Property(x => x.RowVersion).IsRowVersion();
            else entity.Ignore(x => x.RowVersion);
            // Race-safe: only one non-cancelled/rejected booking may own a slot.
            entity.HasIndex(x => x.AvailabilitySlotId).IsUnique()
                .HasFilter("[Status] IN (0, 1, 2, 3)");
            entity.HasIndex(x => new { x.PatientUserId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Nurse).WithMany().HasForeignKey(x => x.NurseProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.AvailabilitySlotId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.CareServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NurseReview>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.BookingId).IsUnique();
            entity.HasIndex(x => new { x.NurseProfileId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PatientUserId, x.CreatedAtUtc });
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            entity.HasOne(x => x.Booking).WithOne().HasForeignKey<NurseReview>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<NurseProfile>().WithMany().HasForeignKey(x => x.NurseProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PatientNotes).HasMaxLength(1000);
            entity.Property(x => x.OriginalFileName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(36).IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
            entity.HasIndex(x => new { x.PatientUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PharmacyProfileId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Pharmacy).WithMany().HasForeignKey(x => x.PharmacyProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PharmacyOrder>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PrescriptionId).IsUnique();
            entity.HasIndex(x => new { x.PatientUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PharmacyProfileId, x.CreatedAtUtc });
            entity.Property(x => x.QuoteAmount).HasPrecision(18, 2);
            entity.Property(x => x.QuoteNotes).HasMaxLength(1500);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Prescription).WithOne().HasForeignKey<PharmacyOrder>(x => x.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PharmacyProfile>().WithMany().HasForeignKey(x => x.PharmacyProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Order_NonnegativeQuote", "[QuoteAmount] > 0"));
        });
        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(800).IsRequired();
            entity.Property(x => x.ReferenceType).HasMaxLength(60);
            entity.HasIndex(x => new { x.RecipientUserId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PatientAddress>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(50).IsRequired();
            entity.Property(x => x.City).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FullAddress).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(300);
            entity.HasIndex(x => new { x.PatientUserId, x.IsDefault })
                .IsUnique().HasFilter("[IsDefault] = 1");
            entity.HasIndex(x => new { x.PatientUserId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.BookingAddress);
        });
        modelBuilder.Entity<FavoriteNurse>(entity =>
        {
            entity.HasKey(x => new { x.PatientUserId, x.NurseProfileId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PatientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Nurse).WithMany().HasForeignKey(x => x.NurseProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => new { x.OwnerUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.SupportTicketId, x.CreatedAtUtc });
            entity.HasOne<SupportTicket>().WithMany(x => x.Messages)
                .HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(p => p.ClrType == typeof(DateTime)))
            {
                property.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
                    value => DateTime.SpecifyKind(value, DateTimeKind.Utc)));
            }
        }
    }
}
