using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        public IActionResult Index()
        {
            var rides = _context.Rides.ToList();
            return View(rides);
        }

        // GET: /Ride/RequestRide
        public IActionResult RequestRide()
        {
            return View();
        }

        // POST: /Ride/RequestRide
        [HttpPost]
        public async Task<IActionResult> RequestRide(Ride ride)
        {
            if (ModelState.IsValid)
            {
                // Automatically assign the logged-in user as the passenger
                var user = await _userManager.GetUserAsync(User);
                ride.PassengerId = user.Id;

                _context.Rides.Add(ride);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
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
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var ride = _context.Rides.FirstOrDefault(r => r.Id == id && r.PassengerId == user.Id);

            if (ride != null)
            {
                _context.Rides.Remove(ride);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}