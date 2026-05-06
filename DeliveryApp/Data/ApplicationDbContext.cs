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
    }
}