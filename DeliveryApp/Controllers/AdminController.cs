using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        // We inject BOTH the database and the UserManager
        public AdminController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/Index (Dashboard)
        public IActionResult Index()
        {
            // 1. ORIGINAL METRICS (Revenue, Totals)
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalRides = _context.Rides.Count();
            ViewBag.TotalRevenue = _context.Rides.Where(r => r.Status == "Completed").Sum(r => r.Price);

            // 2. NEW METRICS (Driver Leaderboard)
            var driverStats = _context.Rides
                .Include(r => r.Driver)
                .ThenInclude(d => d.DriverVehicle)
                .Where(r => r.DriverRating != null)
                .AsEnumerable()
                .GroupBy(r => new {
                    r.DriverId,
                    r.Driver.Name,
                    CarMake = r.Driver.DriverVehicle != null ? r.Driver.DriverVehicle.Make : "Unknown",
                    CarModel = r.Driver.DriverVehicle != null ? r.Driver.DriverVehicle.Model : "Vehicle"
                })
                .Select(g => new
                {
                    DriverName = g.Key.Name,
                    CarModel = g.Key.CarMake + " " + g.Key.CarModel,
                    AverageRating = g.Average(r => r.DriverRating.Value),
                    TotalTrips = g.Count()
                })
                .OrderByDescending(s => s.AverageRating)
                .ToList();

            ViewBag.DriverStats = driverStats;

            // 3. RECENT REVIEWS (Passed as the main Model)
            var ratedRides = _context.Rides
                .Include(r => r.Driver)
                .Include(r => r.Passenger)
                .Where(r => r.DriverRating != null)
                .OrderByDescending(r => r.Id)
                .Take(10) // Just grab the 10 most recent reviews so the page doesn't get too long
                .ToList();

            return View(ratedRides);
        }

        // GET: Admin/AllUsers
        public async Task<IActionResult> AllUsers()
        {
            var allUsers = await _userManager.Users
                .Where(u => u.UserType == "Passenger" || u.UserType == "Driver")
                .ToListAsync();

            var userViewModels = new List<AdminUserViewModel>();

            foreach (var user in allUsers)
            {
                int passengerCount = await _context.Rides.CountAsync(r => r.PassengerId == user.Id);
                int driverCount = await _context.Rides.CountAsync(r => r.DriverId == user.Id);

                bool isBanned = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;

                var vm = new AdminUserViewModel
                {
                    User = user,
                    PassengerRideCount = passengerCount,
                    DriverRideCount = driverCount,
                    IsBanned = isBanned
                };

                userViewModels.Add(vm);
            }

            return View(userViewModels);
        }

        // POST: Admin/BanUser
        [HttpPost]
        public async Task<IActionResult> BanUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SECURITY: Stop the Admin from banning themselves!
            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = "Security Alert: You cannot ban your own Admin account!";
                return RedirectToAction(nameof(AllUsers));
            }

            
            bool isActiveOnRide = await _context.Rides.AnyAsync(r =>
                (r.PassengerId == user.Id || r.DriverId == user.Id) &&
                r.Status != "Completed" &&
                r.Status != "Cancelled");

            if (isActiveOnRide)
            {
                // Block the ban and warn the admin
                TempData["ErrorMessage"] = $"Stop! Cannot ban {user.Name}. They are currently involved in an active ride. You must wait for the trip to finish or cancel it first.";
                return RedirectToAction(nameof(AllUsers));
            }

            // If they pass the checks, drop the ban hammer!
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100)); // 100-year ban

            TempData["SuccessMessage"] = $"{user.Name} has been successfully banned and locked out.";

            return RedirectToAction(nameof(AllUsers));
        }

        // POST: Admin/UnbanUser
        [HttpPost]
        public async Task<IActionResult> UnbanUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.UpdateSecurityStampAsync(user);
                TempData["SuccessMessage"] = $"{user.Name} has been unbanned.";
            }
            return RedirectToAction(nameof(AllUsers));
        }

        // GET: Admin/AllRides
        public async Task<IActionResult> AllRides()
        {
            var rides = await _context.Rides
                .Include(r => r.Passenger)
                .Include(r => r.Driver)
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return View(rides);
        }

        // POST: Admin/CancelRide
        [HttpPost]
        public async Task<IActionResult> CancelRide(int id)
        {
            var ride = await _context.Rides.FindAsync(id);
            if (ride != null && ride.Status != "Completed" && ride.Status != "Cancelled")
            {
                // Clean up any pending offers on this ride before cancelling
                var offers = _context.RideOffers.Where(o => o.RideId == id);
                _context.RideOffers.RemoveRange(offers);

                ride.Status = "Cancelled";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ride #{id} has been cancelled.";
            }
            return RedirectToAction(nameof(AllRides));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            // 1. Find the user
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // 2. SECURITY: Stop the Admin from deleting themselves!
            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = "Security Alert: You cannot delete your own Admin account!";
                return RedirectToAction(nameof(AllUsers));
            }

           
            // We check if they are the passenger OR the driver on an active trip!
            bool isActiveOnRide = await _context.Rides.AnyAsync(r =>
                (r.PassengerId == user.Id || r.DriverId == user.Id) &&
                r.Status != "Completed" &&
                r.Status != "Cancelled");

            if (isActiveOnRide)
            {
                // Block the deletion and warn the admin!
                TempData["ErrorMessage"] = $"Stop! Cannot delete {user.Name}. They are currently involved in an active ride. You must wait for the trip to finish or cancel it first.";
                return RedirectToAction(nameof(AllUsers));
            }
    
            // 3. Attempt the Hard Delete
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.Name} has been permanently deleted from the database.";
            }
            else
            {
                // If it fails (because of completed ride history), show an error
                TempData["ErrorMessage"] = $"Failed to delete {user.Name}. They likely have completed ride history tied to their account. Please ban them instead to preserve records.";
            }

            return RedirectToAction(nameof(AllUsers));
        }
    }
}