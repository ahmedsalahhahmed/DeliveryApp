using System.ComponentModel.DataAnnotations;

namespace DeliveryApp.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        public string DriverId { get; set; }
        public AppUser Driver { get; set; }

        // We will enforce this via a dropdown in the UI
        [Required(ErrorMessage = "Please select a valid manufacturer.")]
        public string Make { get; set; }

        [Required(ErrorMessage = "Please enter the car model.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Model name is too short or too long.")]
        public string Model { get; set; }

        [Required]
        public string Color { get; set; }

        // REGEX FOR EGYPT: 
        // [\u0600-\u06FF\s]{2,5} -> Allows 2 to 3 Arabic letters (with optional spaces between them)
        // [0-9\u0660-\u0669]{3,4} -> Allows 3 to 4 numbers (both English 123 and Arabic ١٢٣ format)
        [Required(ErrorMessage = "License plate is required.")]
        [RegularExpression(@"^[\u0600-\u06FF\s]{2,5}[0-9\u0660-\u0669]{3,4}$",
            ErrorMessage = "Invalid format. Must be 2-3 Arabic letters followed by 3-4 numbers (e.g., أ ب ج 1234).")]
        public string LicensePlate { get; set; }
    }
}