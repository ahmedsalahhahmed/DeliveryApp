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

        public AdminController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Admin/Index (Dashboard)
        public IActionResult Index()
        {
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalRides = _context.Rides.Count();
            ViewBag.TotalRevenue = _context.Rides.Where(r => r.Status == "Completed").Sum(r => r.Price);

            return View();
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