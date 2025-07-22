using LineBotMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;

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
