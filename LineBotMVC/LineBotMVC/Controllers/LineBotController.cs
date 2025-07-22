using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using LineBotMVC.Models; // ใช้ namespace ของคุณ
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace LineBotMVC.Controllers
{
    public class LineBotController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LineBotController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FetchDisplayName(string channelAccessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", channelAccessToken);
            var response = await client.GetAsync("https://api.line.me/v2/bot/info");

            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "ไม่สามารถดึงข้อมูลจาก LINE API ได้" });

            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json);
            string displayName = data["displayName"]?.ToString();

            return Json(new { success = true, displayName });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LineBot model)
        {
            if (string.IsNullOrEmpty(model.ChannelAccessToken) || string.IsNullOrEmpty(model.DisplayName))
            {
                ModelState.AddModelError("", "กรุณากรอกข้อมูลให้ครบ");
                return View(model);
            }

            model.UserName = HttpContext.Session.GetString("UserName");
            model.CreateDate = DateTime.Now;

            _context.LineBots.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "เพิ่ม LineBot เรียบร้อยแล้ว!";
            return RedirectToAction("Index", "BotCommand");
        }
    }
}
