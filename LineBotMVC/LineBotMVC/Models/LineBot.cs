namespace LineBotMVC.Models
{
    public class LineBot
    {
        public int Id { get; set; }
        public string ChannelAccessToken { get; set; }
        public string DisplayName { get; set; }
        public string UserName { get; set; }
        public DateTime CreateDate { get; set; }
        public string ChannelSecret { get; set; }

    }

}
