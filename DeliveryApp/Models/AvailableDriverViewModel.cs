namespace DeliveryApp.Models
{
    public class AvailableDriverViewModel
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public string ProfileImagePath { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }

        public decimal OfferedPrice { get; set; }

        // NEW: Show their reputation!
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
}