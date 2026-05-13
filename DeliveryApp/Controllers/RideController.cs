using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryApp.Controllers
{
    [Authorize] // This protects the entire controller. You MUST be logged in to use it!
    public class RideController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public RideController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user != null && user.UserType == "Driver")
            {
                ViewBag.HasVehicle = _context.Vehicles.Any(v => v.DriverId == user.Id);
                ViewBag.IsBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");

                
                var myRatings = _context.Rides
                    .Where(r => r.DriverId == user.Id && r.DriverRating != null)
                    .Select(r => (double?)r.DriverRating)
                    .ToList();

                if (myRatings.Any())
                {
                    ViewBag.AverageRating = myRatings.Average() ?? 0.0;
                    ViewBag.TotalReviews = myRatings.Count();
                }
                else
                {
                    ViewBag.AverageRating = 0.0;
                    ViewBag.TotalReviews = 0;
                }
            }

            var rides = _context.Rides
                .Include(r => r.Driver)
                .ThenInclude(d => d.DriverVehicle)
                .ToList();

            return View(rides);
        }

        // Request a Ride (Passenger)
        [HttpGet]
        public async Task<IActionResult> RequestRide()
        {
            var user = await _userManager.GetUserAsync(User);
            var activeRide = _context.Rides.FirstOrDefault(r => r.PassengerId == user.Id && r.Status != "Completed" && r.Status != "Cancelled");

            if (activeRide != null) return RedirectToAction(nameof(Index));

            return View(new Ride());
        }

        [HttpPost]
        public async Task<IActionResult> RequestRide(Ride ride)
        {
            var user = await _userManager.GetUserAsync(User);

            if (_context.Rides.Any(r => r.PassengerId == user.Id && r.Status != "Completed" && r.Status != "Cancelled"))
            {
                return RedirectToAction(nameof(Index));
            }

            ride.PassengerId = user.Id;

            // Ignore these so ModelState.IsValid passes!
            ModelState.Remove("PassengerId");
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");
            ModelState.Remove("DriverId");
            ModelState.Remove("Price");
            ModelState.Remove("Status");

            if (ModelState.IsValid)
            {
                double distance = ride.DistanceKm ?? 0;

                if (ride.DeliveryType == "Package")
                {
                    double weight = ride.EstimatedWeightKg ?? 0;
                    double volume = (ride.LengthCm ?? 0) * (ride.WidthCm ?? 0) * (ride.HeightCm ?? 0);
                    ride.Price = 5.00m + (decimal)(weight * 2.0) + (decimal)(volume * 0.001) + (decimal)(distance * 1.0);
                }
                else
                {
                    ride.Price = 5.00m + (decimal)(distance * 1.50);
                }

                ride.Status = "Requested";
                _context.Rides.Add(ride);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(ride);
        }

        // Bidding System
        [HttpPost]
        public async Task<IActionResult> MakeOffer(int rideId, decimal offeredPrice)
        {
            var user = await _userManager.GetUserAsync(User);

            // Do they have a car?
            var hasCar = _context.Vehicles.Any(v => v.DriverId == user.Id);
            if (!hasCar) return RedirectToAction("RegisterCar", "Vehicle");

            
            var isBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");
            if (isBusy)
            {
                TempData["ErrorMessage"] = "You cannot bid on new rides while you have an active passenger!";
                return RedirectToAction(nameof(Index));
            }
            

            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            if (ride == null) return NotFound();

            // SECURITY: Prevent price gouging (Max 20% markup)
            decimal maxAllowedPrice = Math.Round(ride.Price * 1.20m, 2);
            if (offeredPrice < ride.Price || offeredPrice > maxAllowedPrice)
            {
                TempData["ErrorMessage"] = $"Your offer was rejected. Bids must be strictly between ${ride.Price:0.00} and ${maxAllowedPrice:0.00}.";
                return RedirectToAction(nameof(Index));
            }

            if (!_context.RideOffers.Any(o => o.RideId == rideId && o.DriverId == user.Id))
            {
                var offer = new RideOffer { RideId = rideId, DriverId = user.Id, OfferedPrice = offeredPrice };
                _context.RideOffers.Add(offer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public IActionResult ViewOffers(int rideId)
        {
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            if (ride == null) return NotFound();

            var offers = _context.RideOffers
                .Where(o => o.RideId == rideId)
                .Select(o => new AvailableDriverViewModel
                {
                    DriverId = o.Driver.Id,
                    Name = o.Driver.Name,
                    ProfileImagePath = o.Driver.ProfileImagePath,
                    CarMake = _context.Vehicles.Where(v => v.DriverId == o.DriverId).Select(v => v.Make).FirstOrDefault() ?? "Unknown",
                    CarModel = _context.Vehicles.Where(v => v.DriverId == o.DriverId).Select(v => v.Model).FirstOrDefault() ?? "Vehicle",
                    OfferedPrice = o.OfferedPrice,

                    
                    AverageRating = _context.Rides.Where(r => r.DriverId == o.DriverId && r.DriverRating != null)
                                                  .Select(r => (double?)r.DriverRating)
                                                  .Average() ?? 0.0,

                    TotalReviews = _context.Rides.Count(r => r.DriverId == o.DriverId && r.DriverRating != null)
                }).ToList();

            ViewBag.RideId = rideId;
            ViewBag.CalculatedPrice = ride.Price;

            return View("SelectDriver", offers);
        }

        [HttpPost]
        public async Task<IActionResult> AssignDriver(int rideId, string driverId)
        {
            
            var driverIsBusy = _context.Rides.Any(r => r.DriverId == driverId && r.Status == "Driver Assigned");
            if (driverIsBusy)
            {
                TempData["ErrorMessage"] = "Sorry! This driver just accepted another job. Please choose someone else.";
                return RedirectToAction(nameof(Index));
            }
            

            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            var acceptedOffer = _context.RideOffers.FirstOrDefault(o => o.RideId == rideId && o.DriverId == driverId);

            if (ride != null && acceptedOffer != null)
            {
                ride.DriverId = driverId;
                ride.Status = "Driver Assigned";
                ride.Price = acceptedOffer.OfferedPrice; // Update official price to the winning bid!

                var relatedOffers = _context.RideOffers.Where(o => o.RideId == rideId);
                _context.RideOffers.RemoveRange(relatedOffers);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRide(int rideId)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId && r.DriverId == user.Id);

            if (ride != null)
            {
                ride.Status = "Completed";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

       

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride == null) return NotFound();

            return View(ride);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride != null)
            {

                if (ride.Status != "Requested")
                {
                    TempData["ErrorMessage"] = "You cannot cancel a ride after a driver has been assigned!";
                    return RedirectToAction(nameof(Index));
                }

                var attachedOffers = _context.RideOffers.Where(o => o.RideId == id).ToList();
                if (attachedOffers.Any())
                {
                    _context.RideOffers.RemoveRange(attachedOffers);
                }

                _context.Rides.Remove(ride);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }


        // GET: Passenger clicks "Rate Driver"
        [HttpGet]
        public async Task<IActionResult> RateDriver(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.Include(r => r.Driver).FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride == null || ride.Status != "Completed") return NotFound();
            if (ride.DriverRating != null) return RedirectToAction(nameof(Index)); // Already rated!

            return View(ride);
        }

        // POST: Passenger submits the stars
        [HttpPost]
        public async Task<IActionResult> RateDriver(int id, int rating, string review)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride != null && ride.Status == "Completed")
            {
                // Lock the rating between 1 and 5 just in case of hackers!
                ride.DriverRating = Math.Clamp(rating, 1, 5);
                ride.DriverReview = review;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

    }
}