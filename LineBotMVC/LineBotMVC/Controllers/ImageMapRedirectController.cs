using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace LineBotMVC.Controllers
{
    [Route("uploads/{folderId}/{fileName}/{index}")]
    public class ImageMapRedirectController : Controller
    {
        [HttpGet]
        public IActionResult RedirectImage(string folderId, string fileName, string index)
        {
            // ส่งกลับรูปเดียว ไม่ต้องสนใจ index อื่น
            if (fileName != "1040" || index != "0")
                return NotFound();

            string actualFile = "1040.png";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderId, actualFile);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "image/png");
        }
    }
}
