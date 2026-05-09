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
                .Where(r => r.DriverRating != null)
                .AsEnumerable()
                .GroupBy(r => r.Driver.Name)
                .Select(g => new
                {
                    DriverName = g.Key,
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
            var users = await _userManager.Users.ToListAsync();
            return View(users);
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
    }
}