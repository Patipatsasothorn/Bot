using LineBotMVC.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace LineBotMVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------ LOGIN ------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "กรุณากรอกอีเมลและรหัสผ่าน";
                return RedirectToAction("Login");

            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.Password == password);
            if (user != null)
            {
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserName", user.DisplayName ?? user.Email);

                return RedirectToAction("Index", "BotCommand");
            }
            TempData["Error"] = "อีเมลหรือรหัสผ่านไม่ถูกต้อง";
            return RedirectToAction("Login");

        }


        // ------------------ REGISTER ------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(
            string firstName, string lastName, string username,
            string email,
            string phoneNumber,
            string password, string confirmPassword, bool acceptTerms)
        {
            // Validate input เบื้องต้น
            if (password != confirmPassword)
            {
                ViewBag.Error = "รหัสผ่านไม่ตรงกัน";
                return View();
            }
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                ViewBag.Error = "ชื่อผู้ใช้ต้องมีอย่างน้อย 3 ตัวอักษร";
                return View();
            }
            if (_context.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "อีเมลนี้ถูกใช้งานแล้ว";
                return View();
            }
            if (_context.Users.Any(u => u.Username == username))
            {
                ViewBag.Error = "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว";
                return View();
            }
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                if (_context.Users.Any(u => u.PhoneNumber == phoneNumber))
                {
                    ViewBag.Error = "เบอร์โทรศัพท์นี้ถูกใช้งานแล้ว";
                    return View();
                }
            }

            // สร้าง user ใหม่
            var newUser = new User
            {
                Email = email,
                Username = username,
                PhoneNumber = phoneNumber,
                Password = password, // แนะนำเข้ารหัสรหัสผ่านก่อนเก็บจริง
                DisplayName = $"{firstName} {lastName}".Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            ViewBag.Success = "สมัครสมาชิกสำเร็จ กรุณาเข้าสู่ระบบ";

            return View();
        }
        // GET: /Account/Profile
        public IActionResult Profile()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
            {
                // ถ้ายังไม่ล็อกอิน ให้ไปหน้า Login
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                // กรณีไม่พบ user
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // POST: /Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(int id, string email, string phoneNumber, string password, string confirmPassword)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                ViewBag.Error = "ไม่พบข้อมูลผู้ใช้";
                return View("Profile", user);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "กรุณากรอกอีเมล";
                return View("Profile", user);
            }

            // เช็ค email ซ้ำ (ถ้าแก้ไข)
            if (email != user.Email && _context.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "อีเมลนี้ถูกใช้งานแล้ว";
                return View("Profile", user);
            }

            if (!string.IsNullOrEmpty(password))
            {
                if (password != confirmPassword)
                {
                    ViewBag.Error = "รหัสผ่านไม่ตรงกัน";
                    return View("Profile", user);
                }
                // แนะนำเข้ารหัสรหัสผ่านที่นี่ก่อนเก็บจริง (ตอนนี้เก็บ plaintext ชั่วคราว)
                user.Password = password;
            }

            user.Email = email;
            user.PhoneNumber = phoneNumber;

            _context.SaveChanges();

            // อัปเดต session ใหม่ถ้าเปลี่ยนอีเมล
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.DisplayName ?? user.Email);

            ViewBag.Success = "อัปเดตข้อมูลสำเร็จ";
            return View("Profile", user);
        }
        // ------------------ LOGOUT ------------------
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
