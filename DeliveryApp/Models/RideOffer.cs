namespace DeliveryApp.Models
{
    public class RideOffer
    {
        public int Id { get; set; }

        public int RideId { get; set; }
        public Ride Ride { get; set; }

        public string DriverId { get; set; }
        public AppUser Driver { get; set; }

        public decimal OfferedPrice { get; set; }
    }
}