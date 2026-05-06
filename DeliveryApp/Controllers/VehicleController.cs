using DeliveryApp.Data;
using DeliveryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryApp.Controllers
{
    [Authorize] // You must be logged in to register a car!
    public class VehicleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public VehicleController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Shows the empty form to the user
        [HttpGet]
        public async Task<IActionResult> RegisterCar()
        {
            var user = await _userManager.GetUserAsync(User);

            // SECURITY CHECK: Kick them out if they aren't a driver!
            if (user == null || user.UserType != "Driver")
            {
                // You can send them back to the dashboard if they try to sneak in
                return RedirectToAction("Index", "Ride");
            }

            return View();
        }

        // POST: Catches the data when they click Submit
        [HttpPost]
       
        public async Task<IActionResult> RegisterCar(Vehicle vehicle)
        {
            var user = await _userManager.GetUserAsync(User);

            // SECURITY CHECK: Check again on the POST request!
            if (user == null || user.UserType != "Driver")
            {
                return RedirectToAction("Index", "Ride");
            }

            var existingVehicle = _context.Vehicles.FirstOrDefault(v => v.DriverId == user.Id);
            if (existingVehicle != null)
            {
                return RedirectToAction("Index", "Ride");
            }

            vehicle.DriverId = user.Id;
            ModelState.Remove("DriverId");
            ModelState.Remove("Driver");

            if (ModelState.IsValid)
            {
                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Ride");
            }

            return View(vehicle);
        }
        // GET: Vehicle/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.UserType != "Driver") return RedirectToAction("Index", "Ride");

            var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == id && v.DriverId == user.Id);
            if (vehicle == null) return NotFound();

            return View(vehicle);
        }

        // POST: Vehicle/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Vehicle vehicle)
        {
            if (id != vehicle.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.UserType != "Driver") return RedirectToAction("Index", "Ride");

            vehicle.DriverId = user.Id;
            ModelState.Remove("DriverId");
            ModelState.Remove("Driver");

            if (ModelState.IsValid)
            {
                _context.Vehicles.Update(vehicle);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Ride");
            }
            return View(vehicle);
        }

        // GET: Vehicle/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == id && v.DriverId == user.Id);

            if (vehicle == null) return NotFound();

            return View(vehicle);
        }

        // POST: Vehicle/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == id && v.DriverId == user.Id);

            if (vehicle != null)
            {
                _context.Vehicles.Remove(vehicle);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Ride");
        }

        // GET: Vehicle/Manage
        // This is a "smart" button that figures out where the driver needs to go
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            var user = await _userManager.GetUserAsync(User);

            // Security check
            if (user == null || user.UserType != "Driver")
            {
                return RedirectToAction("Index", "Ride");
            }

            // Look for their car
            var vehicle = _context.Vehicles.FirstOrDefault(v => v.DriverId == user.Id);

            if (vehicle == null)
            {
                // If they haven't registered a car yet, send them to the Registration form
                return RedirectToAction(nameof(RegisterCar));
            }

            // If they DO have a car, automatically send them to the Edit form with their Car's ID!
            return RedirectToAction(nameof(Edit), new { id = vehicle.Id });
        }

    }
}
