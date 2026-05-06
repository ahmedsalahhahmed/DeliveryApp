using System.ComponentModel.DataAnnotations;

namespace DeliveryApp.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        // This links the car to a specific user (the Driver)
        [Required]
        public string DriverId { get; set; }
        public AppUser Driver { get; set; }

        [Required(ErrorMessage = "Car Make is required (e.g., Toyota)")]
        public string Make { get; set; }

        [Required(ErrorMessage = "Car Model is required (e.g., Camry)")]
        public string Model { get; set; }

        [Required]
        public string Color { get; set; }

        [Required]
        public string LicensePlate { get; set; }
    }
}