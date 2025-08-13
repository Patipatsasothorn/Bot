using LineBotMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using System.Drawing;

namespace LineBotMVC.Controllers
{
    public class BotCommandController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BotCommandController(ApplicationDbContext context)
        {
            _context = context;
        }
        // แสดงคำสั่งของผู้ใช้ที่ล็อกอิน (กรอง CreateBy)
        public async Task<IActionResult> Index(string selectedBotLineToken = null)
        {
            var currentUser = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUser))
            {
                return RedirectToAction("Login", "Account");
            }

            // ดึงข้อมูล bot lines จากตาราง LineBots ตาม UserName ปัจจุบัน
            var botLines = await _context.LineBots
                .Where(lb => lb.UserName == currentUser)
                .Select(lb => new
                {
                    BotLineName = lb.DisplayName,
                    BotLineToken = lb.ChannelAccessToken
                })
                .ToListAsync();

            ViewBag.BotLines = botLines;
            ViewBag.SelectedBotLineToken = selectedBotLineToken;

            List<BotCommand> commands = new List<BotCommand>();

            if (!string.IsNullOrEmpty(selectedBotLineToken))
            {
                commands = await _context.BotCommands
                    .Where(c => c.CreateBy == currentUser && c.BotLineToken == selectedBotLineToken)
                    .ToListAsync();
            }

            return View(commands);
        }



        // GET: BotCommand/Create
        [HttpGet]
        public async Task<IActionResult> Create(string selectedBotLineToken = null)
        {
            var currentUser = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUser))
            {
                return RedirectToAction("Login", "Account");
            }

            var botLines = await _context.LineBots
                .Where(lb => lb.UserName == currentUser)
                .Select(lb => new
                {
                    BotLineName = lb.DisplayName,
                    BotLineToken = lb.ChannelAccessToken
                })
                .ToListAsync();

            ViewBag.BotLines = botLines;

            return View();
        }
        // POST: BotCommand/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BotCommand model)
        {
            var currentUser = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUser))
            {
                return RedirectToAction("Login", "Account");
            }

            model.CreateBy = currentUser;
            model.UpdatedAt = DateTime.Now;

            _context.BotCommands.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> UploadImagemap(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return Json(new { success = false, message = "กรุณาเลือกไฟล์รูปภาพ" });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(imageFile.ContentType))
                return Json(new { success = false, message = "รองรับเฉพาะไฟล์ JPG, PNG, WEBP" });

            // สร้างโฟลเดอร์เฉพาะ
            var folderId = Guid.NewGuid().ToString();
            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", folderId);
            Directory.CreateDirectory(uploadFolder);

            // อ่านรูปจาก stream
            using (var stream = imageFile.OpenReadStream())
            using (var image = System.Drawing.Image.FromStream(stream))
            {
                // บันทึกรูป 3 ขนาด
                SaveResizedImage(image, Path.Combine(uploadFolder, "1040.png"), 1040, 1040);
                SaveResizedImage(image, Path.Combine(uploadFolder, "700.png"), 700, 700);
                SaveResizedImage(image, Path.Combine(uploadFolder, "460.png"), 460, 460);
            }

            // baseUrl ต้องเป็น HTTPS และไม่ลงท้ายด้วย /1040
            var baseUrl = $"https://{Request.Host}/uploads/{folderId}";

            return Json(new { success = true, baseUrl });
        }

        // ฟังก์ชันย่อรูป
        private void SaveResizedImage(System.Drawing.Image image, string path, int width, int height)
        {
            using (var newImage = new System.Drawing.Bitmap(width, height))
            using (var g = System.Drawing.Graphics.FromImage(newImage))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(image, 0, 0, width, height);
                newImage.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }




        // POST: BotCommand/UploadImage
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return Json(new { success = false, message = "กรุณาเลือกไฟล์รูปภาพ" });

            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(uploadFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            var imageUrl = Url.Content("~/uploads/" + uniqueFileName);
            return Json(new { success = true, url = imageUrl });
        }

        // (แก้ไข, ลบ ฯลฯ ตามต้องการ)
        // GET: BotCommand/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var command = await _context.BotCommands.FindAsync(id);
            if (command == null) return NotFound();

            return View(command);
        }

        // POST: BotCommand/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Command,ResponseType,ResponseText,ImagesJson")] BotCommand command)
        {
            if (id != command.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(command);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(command);
        }
        // GET: BotCommand/CreateTelegramC
        [HttpGet]
        public async Task<IActionResult> CreateTelegramC()
        {
            var currentUser = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUser))
            {
                return RedirectToAction("Login", "Account");
            }

            //// ดึงข้อมูล TelegramBots (คุณอาจจะใช้ตารางแยกต่างหากจาก LineBots)
            //var telegramBots = await _context.TelegramBots
            //    .Where(t => t.UserName == currentUser)
            //    .Select(t => new
            //    {
            //        BotName = t.DisplayName,
            //        BotToken = t.BotToken
            //    })
            //    .ToListAsync();

            //ViewBag.TelegramBots = telegramBots;

            return View("CreateTelegramC"); // เรียก View ที่อยู่ใน Views/BotCommand/CreateTelegramC.cshtml
        }

        // POST: BotCommand/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var command = await _context.BotCommands.FindAsync(id);
            if (command != null)
            {
                _context.BotCommands.Remove(command);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
