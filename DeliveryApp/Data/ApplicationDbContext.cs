using DeliveryApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeliveryApp.Data
{
    // Change the inheritance to use <AppUser> instead of the default IdentityUser
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add your Ride table
        public DbSet<Ride> Rides { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<RideOffer> RideOffers { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            // CRITICAL: You must call the base method first so Identity doesn't break!
            base.OnModelCreating(builder);

            // Tell SQL Server NOT to cascade delete when a Ride is deleted
            builder.Entity<RideOffer>()
                .HasOne(ro => ro.Ride)
                .WithMany()
                .HasForeignKey(ro => ro.RideId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tell SQL Server NOT to cascade delete when a Driver is deleted
            builder.Entity<RideOffer>()
                .HasOne(ro => ro.Driver)
                .WithMany()
                .HasForeignKey(ro => ro.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Vehicle>()
                .HasIndex(v => v.LicensePlate)
                .IsUnique();
        }

    }
}