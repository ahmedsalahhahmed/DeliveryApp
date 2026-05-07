using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryApp.Controllers
{
    [Authorize]
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
            if (user == null) return Challenge();

            IQueryable<Ride> ridesQuery = _context.Rides.Include(r => r.Passenger).Include(r => r.Driver);

            if (user.UserType == "Passenger")
            {
                ridesQuery = ridesQuery.Where(r => r.PassengerId == user.Id);
            }
            else if (user.UserType == "Driver")
            {
                ridesQuery = ridesQuery.Where(r => r.DriverId == user.Id);
            }

            var rides = await ridesQuery.ToListAsync();
            return View(rides);
        }

        // 1. الأكشن اللي بيفتح الصفحة (GET) - واحد فقط!
        [HttpGet]
        public IActionResult RequestRide()
        {
            return View();
        }

        // 2. الأكشن اللي بيستقبل البيانات (POST) - واحد فقط!
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRide(Ride ride)
        {
            var user = await _userManager.GetUserAsync(User);

            // تعيين البيانات الأساسية يدوياً
            ride.PassengerId = user?.Id;
            ride.Status = "Requested";

            // حذف الحقول التي تسبب فشل الـ Validation لأنها Navigation Properties
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");
            ModelState.Remove("PassengerId");

            if (ModelState.IsValid)
            {
                _context.Rides.Add(ride);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // لو فشل، هيطبع الأسباب في الـ Output Window تحت عشان تعرف العيب فين
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Error in {state.Key}: {error.ErrorMessage}");
                }
            }

            return View(ride);
        }

        // GET: /Ride/AvailableRides
        public async Task<IActionResult> AvailableRides()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.UserType != "Driver") return RedirectToAction("Index");

            var rides = await _context.Rides
                .Include(r => r.Passenger)
                .Where(r => r.Status == "Requested")
                .ToListAsync();

            return View(rides);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRide(int id)
        {
            var driver = await _userManager.GetUserAsync(User);
            var ride = await _context.Rides.FindAsync(id);

            if (ride == null || ride.Status != "Requested") return NotFound();

            ride.DriverId = driver.Id;
            ride.Status = "Accepted";

            _context.Rides.Update(ride);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(AvailableRides));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRide(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = await _context.Rides.FindAsync(id);

            if (ride != null && ride.DriverId == user.Id)
            {
                ride.Status = "Completed";
                _context.Rides.Update(ride);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index)); // حولته لـ Index عشان يشوف النتيجة
        }

        // Edit & Delete (تركتهما كما هما مع التأكد من عدم التكرار)
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);
            if (ride == null || ride.Status != "Requested") return NotFound();
            return View(ride);
        }

        [HttpPost]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> Edit(int id, Ride ride)
        {
            if (id != ride.Id) return NotFound();

            
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            
            ride.PassengerId = user.Id;

          
            if (string.IsNullOrEmpty(ride.Status))
            {
                ride.Status = "Requested";
            }

            
            ModelState.Remove("Passenger");
            ModelState.Remove("Driver");
            ModelState.Remove("PassengerId");

            if (ModelState.IsValid)
            {
                try
                {
                    
                    _context.Rides.Update(ride);
                    await _context.SaveChangesAsync();

                  
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Rides.Any(e => e.Id == ride.Id)) return NotFound();
                    else throw;
                }
            }

           
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                Console.WriteLine("Edit Error: " + error.ErrorMessage);
            }

            return View(ride);
        }
    }
}