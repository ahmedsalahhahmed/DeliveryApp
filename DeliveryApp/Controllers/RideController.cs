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

        // ==========================================
        // 1. DASHBOARD
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user != null && user.UserType == "Driver")
            {
                ViewBag.HasVehicle = _context.Vehicles.Any(v => v.DriverId == user.Id);
                ViewBag.IsBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");
            }

            var rides = _context.Rides
                .Include(r => r.Driver)
                .ThenInclude(d => d.DriverVehicle)
                .ToList();

            return View(rides);
        }

        // ==========================================
        // 2. REQUEST A RIDE (PASSENGER)
        // ==========================================
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

        // ==========================================
        // 3. DYNAMIC BIDDING SYSTEM
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> MakeOffer(int rideId, decimal offeredPrice)
        {
            var user = await _userManager.GetUserAsync(User);
            var hasCar = _context.Vehicles.Any(v => v.DriverId == user.Id);
            if (!hasCar) return RedirectToAction("RegisterCar", "Vehicle");

            bool isBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");
            if (isBusy)
            {
                TempData["ErrorMessage"] = "You cannot bid on new jobs while you are on an active trip.";
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

                    OfferedPrice = o.OfferedPrice
                }).ToList();

            ViewBag.RideId = rideId;
            ViewBag.CalculatedPrice = ride.Price;

            return View("SelectDriver", offers);
        }

        [HttpPost]
        public async Task<IActionResult> AssignDriver(int rideId, string driverId)
        {
            // ==========================================
            // NEW: DOUBLE BOOKING DEFENSE
            // Did another passenger hire this driver 5 seconds ago?
            // ==========================================
            bool isDriverBusy = _context.Rides.Any(r => r.DriverId == driverId && r.Status == "Driver Assigned");
            if (isDriverBusy)
            {
                TempData["ErrorMessage"] = "Sorry, this driver just accepted another job! Please select a different driver.";
                return RedirectToAction("ViewOffers", new { rideId = rideId });
            }

            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            var acceptedOffer = _context.RideOffers.FirstOrDefault(o => o.RideId == rideId && o.DriverId == driverId);

            if (ride != null && acceptedOffer != null)
            {
                ride.DriverId = driverId;
                ride.Status = "Driver Assigned";
                ride.Price = acceptedOffer.OfferedPrice;

                // 1. Remove all competing offers for THIS ride (since it is now claimed)
                var relatedOffers = _context.RideOffers.Where(o => o.RideId == rideId);
                _context.RideOffers.RemoveRange(relatedOffers);

                // 2. AUTO-CLEANUP: Remove this driver's bids from all OTHER rides!
                var driversOtherBids = _context.RideOffers.Where(o => o.DriverId == driverId);
                _context.RideOffers.RemoveRange(driversOtherBids);

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

        // ==========================================
        // 4. EDIT & DELETE RIDES
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride == null) return NotFound();
            if (ride.Status != "Requested") return RedirectToAction(nameof(Index));

            return View(ride);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Ride ride)
        {
            if (id != ride.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);

           
            var originalRide = _context.Rides.AsNoTracking().FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            // 2. Check if the ride exists AND if it is still safe to edit
            if (originalRide == null || originalRide.Status != "Requested")
            {
                // Hacker blocked! They tried to edit a ride that is already in progress or completed.
                return Unauthorized();
            }
            

            ride.PassengerId = user.Id;

            // Tell C# to ignore missing fields from the HTML form
            ModelState.Remove("PassengerId");
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");
            ModelState.Remove("Price");
            ModelState.Remove("Status");

            if (ModelState.IsValid)
            {
                ride.Price = originalRide.Price;
                ride.Status = originalRide.Status;
                ride.DistanceKm = originalRide.DistanceKm; // Protect the distance too!

                _context.Rides.Update(ride);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(ride);
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
    }
}