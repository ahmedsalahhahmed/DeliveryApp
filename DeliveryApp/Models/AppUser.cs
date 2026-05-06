using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DeliveryApp.Models
{
    // Inheriting from IdentityUser gives us the Id (string), Email, and Password backend automatically.
    public class AppUser : IdentityUser
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string UserType { get; set; } // "Driver" or "Passenger"[cite: 8]

        public string? ProfileImagePath { get; set; } // Nullable, for requirement 4

        public Vehicle? DriverVehicle { get; set; }
    }
}