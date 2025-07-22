namespace LineBotMVC.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }  // เพิ่มจากตัวอย่างก่อนหน้านี้

        public string Email { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PhoneNumber { get; set; }  // เพิ่มฟิลด์เบอร์โทรศัพท์

    }

}
