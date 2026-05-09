using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryApp.Controllers
{
    // SECURITY: If anyone without the Admin role tries to access this, they get blocked!
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

        // GET: Admin/AllUsers — excludes Admin accounts, includes ride counts + ban state
        public async Task<IActionResult> AllUsers()
        {
            // Only show Passengers and Drivers — not other admins
            var users = await _userManager.Users
                .Where(u => u.UserType == "Passenger" || u.UserType == "Driver")
                .ToListAsync();

            var viewModels = users.Select(u => new AdminUserViewModel
            {
                User = u,
                PassengerRideCount = _context.Rides.Count(r => r.PassengerId == u.Id),
                DriverRideCount = _context.Rides.Count(r => r.DriverId == u.Id),
                IsBanned = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow
            }).ToList();

            return View(viewModels);
        }

        // POST: Admin/BanUser
        [HttpPost]
        public async Task<IActionResult> BanUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                // Lock them out for 100 years — effectively permanent
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["SuccessMessage"] = $"{user.Name} has been banned.";
            }
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
    }
}