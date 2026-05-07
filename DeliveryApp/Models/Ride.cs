using System.ComponentModel.DataAnnotations;

namespace DeliveryApp.Models
{
    public class Ride
    {
        public int Id { get; set; } // [cite: 8]

        // Identity uses string for IDs!
        public string PassengerId { get; set; }
        public AppUser Passenger { get; set; }

        public string? DriverId { get; set; } // Nullable because a driver hasn't accepted yet
        public AppUser Driver { get; set; }

        [Required]
        public string PickupAddress { get; set; }

        [Required]
        public string DestinationAddress { get; set; }
        

        [Required]
        public string DeliveryType { get; set; } = "Passenger"; // "Passenger" or "Package"

        // Package-specific fields
        public string? PackageDescription { get; set; }
        public double? EstimatedWeightKg { get; set; }

        public double? LengthCm { get; set; }
        public double? WidthCm { get; set; }
        public double? HeightCm { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }

        public string Status { get; set; } = "Requested"; //[cite: 8]
        public decimal Price { get; set; }
        //[cite: 8]
    }
}