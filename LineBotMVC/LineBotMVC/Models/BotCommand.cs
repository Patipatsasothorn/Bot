namespace LineBotMVC.Models
{
    public class BotCommand
    {
        public int Id { get; set; }
        public string Command { get; set; }
        public string? ResponseText { get; set; }
        public string ResponseType { get; set; } // text, image, carousel, flex
        public string? ImagesJson { get; set; }   // JSON รูปแบบใหม่ตาม TimeRange
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public string CreateBy { get; set; }      // ผู้สร้างคำสั่ง
        public string BotLineName { get; set; }   // ชื่อบอท LINE
        public string BotLineToken { get; set; }  // Token ของ LINE Bot

    }

}
