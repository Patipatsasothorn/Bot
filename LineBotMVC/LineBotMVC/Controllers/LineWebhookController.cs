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
                                        // ตรวจสอบว่า ImagesJson เป็น array ของ imagemap objects หรือ array ของ string
                                        var jsonData = cmd.ImagesJson;

                                        // ลองแปลงเป็น imagemap objects ก่อน
                                        try
                                        {
                                            var imageMaps = JsonConvert.DeserializeObject<List<dynamic>>(jsonData);

                                            // ตรวจสอบว่า element แรกมี baseUrl หรือไม่ (เป็น imagemap)
                                            if (imageMaps.Count > 0 && imageMaps[0].baseUrl != null)
                                            {
                                                // กรณีเป็น array ของ imagemap objects
                                                var bubbles = imageMaps.Select((imageMap, index) => new
                                                {
                                                    type = "bubble",
                                                    size = "kilo",
                                                    hero = new
                                                    {
                                                        type = "image",
                                                        url = GetImageUrl(imageMap.baseUrl?.ToString()),
                                                        size = "full",
                                                        aspectRatio = "1.51:1",
                                                        aspectMode = "cover",
                                                        action = new
                                                        {
                                                            type = "postback",
                                                            data = $"imagemap_view={index + 1}"
                                                        }
                                                    },
                                                    body = new
                                                    {
                                                        type = "box",
                                                        layout = "vertical",
                                                        spacing = "sm",
                                                        contents = new object[]
                                                        {
                        new {
                            type = "text",
                            text = imageMap.altText?.ToString() ?? $"หน้า {index + 1}",
                            weight = "bold",
                            size = "md",
                            align = "center",
                            color = "#333333"
                        },
                        new {
                            type = "separator",
                            margin = "sm"
                        },
                        new {
                            type = "box",
                            layout = "vertical",
                            spacing = "xs",
                            margin = "sm",
                            contents = CreateButtonRowsFromImageMapActions(imageMap.actions)
                        }
                                                        }
                                                    },
                                                    footer = new
                                                    {
                                                        type = "box",
                                                        layout = "vertical",
                                                        spacing = "sm",
                                                        contents = new object[]
                                                        {
                        new {
                            type = "text",
                            text = $"หน้า {index + 1} • แตะรูปเพื่อดูเต็ม",
                            size = "xs",
                            color = "#999999",
                            align = "center"
                        }
                                                        }
                                                    }
                                                }).ToList();

                                                var replyCarousel = new
                                                {
                                                    replyToken = replyToken,
                                                    messages = new[] {
                    new {
                        type = "flex",
                        altText = "ImageMap Carousel - เลื่อนดูได้",
                        contents = new {
                            type = "carousel",
                            contents = bubbles
                        }
                    }
                }
                                                };

                                                await ReplyFlex(matchedBot.ChannelAccessToken, replyCarousel);
                                            }
                                            else
                                            {
                                                // กรณีเป็น array ของ string URLs (วิธีเดิม)
                                                var images = JsonConvert.DeserializeObject<List<string>>(jsonData);
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
                                        }
                                        catch
                                        {
                                            // ถ้า parse ไม่ได้ ใช้วิธีเดิม
                                            var images = JsonConvert.DeserializeObject<List<string>>(jsonData);
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

                                        // ดึง baseUrl จริงจาก JSON แล้วเติม ?w=auto
                                        string baseUrl = $"{imagemapJson.baseUrl}.png?w=auto";

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
        // Helper method สำหรับสร้าง Button Rows จาก ImageMap Actions
        private object[] CreateButtonRowsFromImageMapActions(dynamic actions)
        {
            if (actions == null)
                return new object[0];

            var buttonRows = new List<object>();
            var currentRowButtons = new List<object>();
            int buttonsPerRow = 4;

            var actionsList = ((IEnumerable<dynamic>)actions).ToList();

            for (int i = 0; i < actionsList.Count; i++)
            {
                var action = actionsList[i];

                // สร้าง button จาก action
                var buttonText = GetButtonTextFromImageMapAction(action, i + 1);

                var button = new
                {
                    type = "button",
                    action = new
                    {
                        type = action.type?.ToString() ?? "postback",
                        data = action.data?.ToString(),
                        uri = action.linkUri?.ToString() ?? action.uri?.ToString()
                    },
                    style = GetButtonStyle(action.type?.ToString()),
                    height = "sm",
                    flex = 1
                };

                currentRowButtons.Add(button);

                // ถ้าครบจำนวนต่อ row หรือเป็น action สุดท้าย
                if (currentRowButtons.Count == buttonsPerRow || i == actionsList.Count - 1)
                {
                    // ถ้า row สุดท้ายมี button น้อยกว่า buttonsPerRow ให้เติมช่องว่าง
                    while (currentRowButtons.Count < buttonsPerRow && i == actionsList.Count - 1)
                    {
                        var spacer = new
                        {
                            type = "spacer",
                            size = "sm"
                        };
                        currentRowButtons.Add(spacer);
                    }

                    // สร้าง row box
                    var row = new
                    {
                        type = "box",
                        layout = "horizontal",
                        spacing = "xs",
                        contents = currentRowButtons.ToArray()
                    };

                    buttonRows.Add(row);
                    currentRowButtons.Clear();
                }
            }

            return buttonRows.ToArray();
        }

        // Helper method สำหรับกำหนด Button Text
        private string GetButtonTextFromImageMapAction(dynamic action, int index)
        {
            // ลองดึง text จาก action ก่อน
            if (action.text != null)
                return action.text.ToString();

            // ถ้าเป็น uri action ลองดึงชื่อจาก domain
            var uri = action.linkUri?.ToString() ?? action.uri?.ToString();
            if (!string.IsNullOrEmpty(uri))
            {
                try
                {
                    var domain = new Uri(uri).Host.Replace("www.", "");
                    var domainNames = new Dictionary<string, string>
            {
                {"google.com", "Google 🔍"},
                {"youtube.com", "YouTube 📺"},
                {"facebook.com", "Facebook 👥"},
                {"instagram.com", "Instagram 📸"},
                {"twitter.com", "Twitter 🐦"},
                {"line.me", "Line 💬"},
                {"github.com", "GitHub 💻"}
            };

                    if (domainNames.ContainsKey(domain))
                        return domainNames[domain];

                    // ถ้าไม่เจอใน mapping ให้ใช้ชื่อ domain
                    return domain.Split('.')[0].ToUpper();
                }
                catch
                {
                    // ถ้า parse URI ไม่ได้
                    return $"ลิงก์ {index}";
                }
            }

            // ถ้าเป็น postback action
            var data = action.data?.ToString();
            if (!string.IsNullOrEmpty(data))
            {
                if (data.StartsWith("game="))
                    return data.Replace("game=", "").ToUpper();
                if (data.StartsWith("action="))
                    return data.Replace("action=", "").ToUpper();
            }

            return $"ปุ่ม {index}";
        }

        // Helper method สำหรับกำหนดสไตล์ปุ่ม
        private string GetButtonStyle(string actionType)
        {
            return actionType?.ToLower() switch
            {
                "uri" => "primary",     // สีน้ำเงินสำหรับลิงก์
                "postback" => "secondary", // สีเทาสำหรับ postback
                _ => "secondary"
            };
        }
        // Helper method สำหรับสร้าง Image URL
        private string GetImageUrl(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return "";

            // ถ้ามี .png แล้วไม่ต้องเติม
            if (baseUrl.EndsWith(".png") || baseUrl.EndsWith(".jpg") || baseUrl.EndsWith(".jpeg"))
                return baseUrl.StartsWith("http") ? baseUrl : $"https://botline.xcoptech.net{baseUrl}";

            // ถ้ายังไม่มี extension ให้เติม .png
            var imageUrl = baseUrl.EndsWith("/") ? $"{baseUrl}1040.png" : $"{baseUrl}.png";
            return imageUrl.StartsWith("http") ? imageUrl : $"https://botline.xcoptech.net{imageUrl}";
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
