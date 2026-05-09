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
        public IActionResult RegisterCar()
        {
            // Send the list of cars to the frontend!
            ViewBag.CarMakes = GetCarMakes();
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

            var plateExists = _context.Vehicles.Any(v => v.LicensePlate == vehicle.LicensePlate);
            if (plateExists)
            {
                // This attaches a red error message directly to the License Plate text box!
                ModelState.AddModelError("LicensePlate", "This plate is already registered");
            }

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

            ViewBag.CarMakes = GetCarMakes();


            return View(vehicle);
        }

        // POST: Vehicle/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Vehicle vehicle)
        {
            if (id != vehicle.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.UserType != "Driver") return RedirectToAction("Index", "Ride");


            var isBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");
            if (isBusy)
            {
                TempData["ErrorMessage"] = "You cannot edit your vehicle details while you have an active passenger!";
                return RedirectToAction("Index", "Ride");
            }

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

            // NEW SECURITY CHECK: Is the driver on a trip right now?
            var isBusy = _context.Rides.Any(r => r.DriverId == user.Id && r.Status == "Driver Assigned");
            if (isBusy)
            {
                TempData["ErrorMessage"] = "You cannot delete your vehicle while you have an active passenger!";
                return RedirectToAction("Index", "Ride");
            }

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

        // A helper method to generate a list of car brands optimized for the Egyptian Market
        private List<string> GetCarMakes()
        {
            var makes = new List<string>
            { 
                // The Absolute Most Popular (Uber/InDrive Staples in Egypt)
                "Hyundai", "Nissan", "Chevrolet", "Kia", "BYD", "Chery", "MG",
                "Renault", "Toyota", "Skoda", "Peugeot", "Fiat", "Lada", "Suzuki", "Mitsubishi",

                // Very Common Daily Drivers
                "Opel", "Seat", "Volkswagen", "Citroen", "Geely", "Mazda", "Honda",
                "Ford", "Subaru", "Proton", "Daihatsu",

                // Chinese & Emerging Brands (Massive growth in Egypt right now)
                "Changan", "Haval", "Jetour", "DFSK", "BAIC", "Brilliance", "FAW",
                "Zotye", "JAC", "Soueast", "Dongfeng", "Exeed", "Bestune", "Speranza", "Senova",

                // Luxury (For Uber Select / Uber Black)
                "Mercedes-Benz", "BMW", "Audi", "Jeep", "Volvo", "Lexus", "Land Rover",
                "Alfa Romeo", "Porsche", "Jaguar", "Mini",

                // American / Heavy Duty
                "Chrysler", "Dodge", "GMC"
                
            };

            makes.Sort(); // Alphabetizes the list automatically!
            return makes;
        }

    }
}
