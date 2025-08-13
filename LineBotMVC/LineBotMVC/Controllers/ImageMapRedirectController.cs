using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace LineBotMVC.Controllers
{
    [Route("imagemap/{folderId}/{fileName}/{index}")]
    public class ImageMapRedirectController : Controller
    {
        [HttpGet]
        public IActionResult RedirectImage(string folderId, string fileName, string index)
        {
            // ตรวจสอบว่ากำหนดไฟล์จริง 1040 เท่านั้น
            if (fileName != "1040")
                return NotFound();

            // ชี้ไปที่ไฟล์จริง
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderId, "1040.png");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "image/png");
        }
    }
}
