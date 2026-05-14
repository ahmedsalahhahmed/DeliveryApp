namespace DeliveryApp.Models
{
    public class AdminUserViewModel
    {
        public AppUser User { get; set; }
        public int PassengerRideCount { get; set; }
        public int DriverRideCount { get; set; }
        public bool IsBanned { get; set; }
    }
}
