using LineBotMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace LineBotMVC.Controllers
{
    public class LineWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LineWebhookController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("line/webhook")]
        public async Task<IActionResult> LineWebhook()
        {
            var xLineSignature = Request.Headers["X-Line-Signature"].FirstOrDefault();
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var lineBots = await _context.LineBots.ToListAsync();

            LineBot matchedBot = null;
            foreach (var bot in lineBots)
            {
                if (ValidateSignature(bot.ChannelSecret, body, xLineSignature))
                {
                    matchedBot = bot;
                    break;
                }
            }

            if (matchedBot == null)
            {
                return BadRequest("Invalid signature");
            }

            dynamic data = JsonConvert.DeserializeObject(body);

            foreach (var evt in data.events)
            {
                string type = evt.type;

                if (type == "message")
                {
                    string messageType = evt.message.type;
                    string replyToken = evt.replyToken;

                    if (messageType == "text")
                    {
                        string userMessage = evt.message.text.ToString().Trim().ToLower();

                        switch (userMessage)
                        {
                            case "สวัสดี":
                                await ReplyText(matchedBot.ChannelAccessToken, replyToken, "สวัสดีค่ะ ยินดีให้บริการค่ะ");
                                break;

                            case "ช่วยด้วย":
                                await ReplyText(matchedBot.ChannelAccessToken, replyToken, "คุณสามารถติดต่อแอดมินได้ที่เบอร์ 099-999-9999");
                                break;

                            case "เมนู":
                                await ReplyText(matchedBot.ChannelAccessToken, replyToken, "เมนูของเรามี: \n- รายงาน\n- สมัครสมาชิก\n- ติดต่อเรา");
                                break;

                            default:
                                string timeRange = GetCurrentTimeRange();

                                var cmd = await _context.BotCommands
                                    .Where(c => c.BotLineName == matchedBot.DisplayName && c.Command.ToLower() == userMessage)
                                    .OrderByDescending(c => c.TimePeriod == timeRange) // ถ้ามี TimeRange ตรงจะมาก่อน
                                    .FirstOrDefaultAsync();

                                if (cmd != null)
                                {
                                    if (cmd.ResponseType == "text")
                                    {
                                        await ReplyText(matchedBot.ChannelAccessToken, replyToken, cmd.ResponseText);
                                    }
                                    else if (cmd.ResponseType == "carousel")
                                    {
                                        var images = JsonConvert.DeserializeObject<List<string>>(cmd.ImagesJson);
                                        var bubbles = images.Select(url => new
                                        {
                                            type = "bubble",
                                            hero = new
                                            {
                                                type = "image",
                                                url = url.StartsWith("http") ? url : $"https://botline.xcoptech.net{url}",
                                                size = "full",
                                                aspectRatio = "20:13",
                                                aspectMode = "cover"
                                            }
                                        }).ToList();

                                        var replyCarousel = new
                                        {
                                            replyToken = replyToken,
                                            messages = new[] {
                                                new {
                                                    type = "flex",
                                                    altText = "ภาพเลื่อน",
                                                    contents = new {
                                                        type = "carousel",
                                                        contents = bubbles
                                                    }
                                                }
                                            }
                                        };

                                        await ReplyFlex(matchedBot.ChannelAccessToken, replyCarousel);
                                    }
                                    else if (cmd.ResponseType == "card")
                                    {
                                        var json = cmd.ImagesJson.Trim();
                                        object contents;

                                        if (json.StartsWith("["))
                                        {
                                            var cardBubbles = JsonConvert.DeserializeObject<List<object>>(json);
                                            contents = new { type = "carousel", contents = cardBubbles };
                                        }
                                        else
                                        {
                                            var singleBubble = JsonConvert.DeserializeObject<object>(json);
                                            contents = singleBubble;
                                        }

                                        var replyCard = new
                                        {
                                            replyToken = replyToken,
                                            messages = new[] {
                                                new {
                                                    type = "flex",
                                                    altText = "Card Message",
                                                    contents = contents
                                                }
                                            }
                                        };

                                        await ReplyFlex(matchedBot.ChannelAccessToken, replyCard);
                                    }
                                    else if (cmd.ResponseType == "imagemap")
                                    {
                                        dynamic imagemapJson = JsonConvert.DeserializeObject<dynamic>(cmd.ImagesJson);

                                        string folderId = "7be0870a-599a-4031-b11b-b645060f3ea5"; // folderId จาก UploadImagemap
                                        string baseUrl = $"https://botline.xcoptech.net/uploads/{folderId}/1040"; // ไม่มี .png


                                        var replyImagemap = new
                                        {
                                            replyToken = replyToken,
                                            messages = new object[]
                                            {
                                            new {
                                                type = "text",
                                                text = $"LINE จะเรียกรูปที่: {baseUrl}"
                                            },
                                            new {
                                                type = "imagemap",
                                                baseUrl = baseUrl,
                                                altText = "ImageMap รูปเดียว",
                                                baseSize = new
                                                {
                                                    width = (int)imagemapJson.baseSize.width,
                                                    height = (int)imagemapJson.baseSize.height
                                                },
                                                actions = imagemapJson.actions
                                            }
                                            }
                                        };

                                        await SendReply(matchedBot.ChannelAccessToken, replyImagemap);
                                    }



                                    else
                                    {
                                        await ReplyText(matchedBot.ChannelAccessToken, replyToken, "คำสั่งนี้ยังไม่รองรับประเภทข้อความนี้ค่ะ");
                                    }
                                }
                                else
                                {
                                    await ReplyText(matchedBot.ChannelAccessToken, replyToken, "ขออภัย ฉันไม่เข้าใจคำสั่งนี้ค่ะ");
                                }
                                break;
                        }
                    }
                    else if (messageType == "sticker")
                    {
                        await ReplyText(matchedBot.ChannelAccessToken, replyToken, "คุณส่งสติกเกอร์มา ขอบคุณค่ะ 😊");
                    }
                    else if (messageType == "image")
                    {
                        await ReplyText(matchedBot.ChannelAccessToken, replyToken, "ได้รับรูปภาพของคุณแล้ว ขอบคุณค่ะ");
                    }
                }
            }

            return Ok();
        }
        private string GetCurrentTimeRange()
        {
            var now = DateTime.Now.TimeOfDay;

            if (now >= new TimeSpan(6, 0, 0) && now < new TimeSpan(12, 0, 0))
                return "morning";
            else if (now >= new TimeSpan(12, 0, 0) && now < new TimeSpan(18, 0, 0))
                return "afternoon";
            else if (now >= new TimeSpan(18, 0, 0) && now <= new TimeSpan(23, 59, 59))
                return "evening";
            else
                return "night";
        }

        // ฟังก์ชันตรวจสอบ signature
        private bool ValidateSignature(string channelSecret, string requestBody, string xLineSignature)
        {
            var key = Encoding.UTF8.GetBytes(channelSecret);
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
            var computedSignature = Convert.ToBase64String(hash);
            return computedSignature == xLineSignature;
        }

        private async Task ReplyText(string channelAccessToken, string replyToken, string message)
        {
            var payload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };
            await SendReply(channelAccessToken, payload);
        }

        private async Task ReplyFlex(string channelAccessToken, object payload)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", channelAccessToken);
            var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine("LINE API Error: " + error);
            }
        }

        private async Task SendReply(string channelAccessToken, object payload)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", channelAccessToken);
            var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine("LINE API Error: " + error);
            }
        }
    }
}
