using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartCalendar.Models;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Event> Events { get; set; }

    public DbSet<Tag> Tags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>()
            .HasMany(e => e.Tags)
            .WithMany(t => t.Events)
            .UsingEntity<Dictionary<string, object>>(
                "EventTags",
                j => j.HasOne<Tag>().WithMany().HasForeignKey("TagsId"),
                j => j.HasOne<Event>().WithMany().HasForeignKey("EventsId"),
                j =>
                {
                    j.HasKey("EventsId", "TagsId"); // ✅ Primary key tanımı burada
                    j.ToTable("EventTags");
                });
    }


}
