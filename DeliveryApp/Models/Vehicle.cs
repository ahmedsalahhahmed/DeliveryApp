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


        [RegularExpression(@"^[a-zA-Z]{3,4}[ -]?[0-9]{3,4}$",
        ErrorMessage = "Invalid format. Use 3-4 English letters followed by 3-4 numbers (e.g., ABC 1234, ABC-1234).")]
       
        public string LicensePlate { get; set; }
    }
}