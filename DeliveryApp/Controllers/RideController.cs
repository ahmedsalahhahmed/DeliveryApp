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

        // GET: /Ride/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Load rides and INCLUDE the Driver and Vehicle data so the passenger can see the license plate later!
            var rides = _context.Rides
                .Include(r => r.Driver)
                .ThenInclude(d => d.DriverVehicle)
                .ToList();

            return View(rides);
        }

        // GET: /Ride/RequestRide
        // GET: RequestRide
        [HttpGet]
        public async Task<IActionResult> RequestRide()
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. Check if they already have an active ride!
            var activeRide = _context.Rides.FirstOrDefault(r => r.PassengerId == user.Id && r.Status != "Completed" && r.Status != "Cancelled");

            if (activeRide != null)
            {
                // If they have an active ride, force them back to the dashboard
                return RedirectToAction(nameof(Index));
            }

            return View(new Ride());
        }

        // POST: RequestRide
        [HttpPost]
        public async Task<IActionResult> RequestRide(Ride ride)
        {
            var user = await _userManager.GetUserAsync(User);

            // 2. Double-check on POST just to be safe from hackers
            if (_context.Rides.Any(r => r.PassengerId == user.Id && r.Status != "Completed" && r.Status != "Cancelled"))
            {
                return RedirectToAction(nameof(Index));
            }

            ride.PassengerId = user.Id;
            ModelState.Remove("PassengerId");
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");

            if (ModelState.IsValid)
            {
                if (ride.DeliveryType == "Package")
                {
                    double weight = ride.EstimatedWeightKg ?? 0;
                    double volume = (ride.LengthCm ?? 0) * (ride.WidthCm ?? 0) * (ride.HeightCm ?? 0);
                    ride.Price = 5.00m + (decimal)(weight * 2.0) + (decimal)(volume * 0.001);
                }
                else
                {
                    ride.Price = 15.00m;
                }

                ride.Status = "Requested"; // Changed back to "Requested" so drivers can see it
                _context.Rides.Add(ride);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(ride);
        }

        // GET: Ride/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            // SECURITY: Only find the ride if it belongs to the logged-in user!
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride == null) return NotFound();

            // Business Rule: Don't let them edit if a driver is already on the way
            if (ride.Status != "Requested")
            {
                // You could redirect to an error page, but for now, back to index
                return RedirectToAction(nameof(Index));
            }

            return View(ride);
        }

        // POST: Ride/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Ride ride)
        {
            if (id != ride.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            ride.PassengerId = user.Id; // Re-assign to prevent hacking the form

            ModelState.Remove("PassengerId");
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");

            if (ModelState.IsValid)
            {
                _context.Rides.Update(ride);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(ride);
        }

        // GET: Ride/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride == null) return NotFound();

            return View(ride);
        }

        // POST: Ride/Delete/5
        // POST: Ride/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride != null)
            {
                // Find all offers attached to this specific ride
                var attachedOffers = _context.RideOffers.Where(o => o.RideId == id).ToList();

                // If there are any offers, delete them so SQL Server doesn't panic
                if (attachedOffers.Any())
                {
                    _context.RideOffers.RemoveRange(attachedOffers);
                }
                // ==========================================

                // Now it is perfectly safe to delete the ride itself
                _context.Rides.Remove(ride);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Shows the list of available drivers
        [HttpGet]
        public IActionResult SelectDriver(int rideId)
        {
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            if (ride == null) return NotFound();

            // Find all Drivers who have registered a Vehicle
            var availableDrivers = _context.Users
                .Where(u => u.UserType == "Driver")
                .Join(_context.Vehicles, user => user.Id, vehicle => vehicle.DriverId, (user, vehicle) => new AvailableDriverViewModel
                {
                    DriverId = user.Id,
                    Name = user.Name,
                    ProfileImagePath = user.ProfileImagePath,
                    CarMake = vehicle.Make,
                    CarModel = vehicle.Model
                }).ToList();

            ViewBag.RideId = rideId;
            ViewBag.CalculatedPrice = ride.Price;

            return View(availableDrivers);
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

        // POST: Driver offers to take a ride
        [HttpPost]
        public async Task<IActionResult> MakeOffer(int rideId)
        {
            var user = await _userManager.GetUserAsync(User);

            // Check if they already made an offer
            if (!_context.RideOffers.Any(o => o.RideId == rideId && o.DriverId == user.Id))
            {
                var offer = new RideOffer { RideId = rideId, DriverId = user.Id };
                _context.RideOffers.Add(offer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Passenger views the drivers who made an offer
        // GET: Passenger clicks "View Offers"
        [HttpGet]
        public IActionResult ViewOffers(int rideId)
        {
            // 1. Get the ride so we can show the price
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            if (ride == null) return NotFound();

            // 2. Find all drivers who made an offer on this specific ride
            var offers = _context.RideOffers
                .Where(o => o.RideId == rideId)
                .Select(o => new AvailableDriverViewModel
                {
                    DriverId = o.Driver.Id,
                    Name = o.Driver.Name,
                    ProfileImagePath = o.Driver.ProfileImagePath,
                    // Look up the vehicle for this specific driver
                    CarMake = _context.Vehicles.FirstOrDefault(v => v.DriverId == o.DriverId).Make,
                    CarModel = _context.Vehicles.FirstOrDefault(v => v.DriverId == o.DriverId).Model
                }).ToList();

            ViewBag.RideId = rideId;
            ViewBag.CalculatedPrice = ride.Price;

            return View("SelectDriver", offers);
        }

        // POST: Passenger clicks "Accept Driver" on a specific card
        [HttpPost]
        public async Task<IActionResult> AssignDriver(int rideId, string driverId)
        {
            var ride = _context.Rides.FirstOrDefault(r => r.Id == rideId);
            if (ride != null)
            {
                // 1. Lock in the driver and update the status!
                ride.DriverId = driverId;
                ride.Status = "Driver Assigned";

                // 2. Clean up the database! Delete all offers for this ride since it is now taken.
                var relatedOffers = _context.RideOffers.Where(o => o.RideId == rideId);
                _context.RideOffers.RemoveRange(relatedOffers);

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }



    }
}