using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using SmartCalendar.Models;
using System;
using System.Text.RegularExpressions;

namespace SmartCalendar.Controllers
{
    public class EventParserController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public EventParserController(IHttpClientFactory httpClientFactory, ApplicationDbContext context, UserManager<User> userManager)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Parse(string sentence)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var client = _httpClientFactory.CreateClient();

            var prompt = $@"
Sadece aşağıdaki formatta geçerli JSON çıktısı ver:

{{
  ""title"": ""Başlık"",
  ""date"": ""2025-07-15"",
  ""time"": ""09:00"",
  ""description"": ""Açıklama"",
  ""category"": ""iş""
}}

Hiçbir açıklama, başlık, örnek veya not yazma. Sadece geçerli JSON döndür. 
Etkinlik cümlesi: ""{sentence}""
";

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5001/api/chat")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { prompt }), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            var parsed = JsonConvert.DeserializeObject<dynamic>(result);
            string raw = parsed?.response ?? "";

            // JSON parçasını güvenli şekilde çıkart (regex ile)
            var match = Regex.Match(raw, @"\{[\s\S]*?\}");
            if (!match.Success)
            {
                ViewBag.Error = "Geçerli JSON bulunamadı.";
                ViewBag.Raw = raw;
                return View("Index");
            }

            JObject jsonOutput;
            try
            {
                jsonOutput = JObject.Parse(match.Value);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "JSON ayrıştırılamadı: " + ex.Message;
                ViewBag.Raw = match.Value;
                return View("Index");
            }

            // Tarih + saat kontrolü
            string date = jsonOutput["date"]?.ToString();
            string time = jsonOutput["time"]?.ToString();
            if (!DateTime.TryParse($"{date} {time}", out DateTime eventDateTime))
            {
                ViewBag.Error = "Tarih veya saat formatı geçersiz.";
                ViewBag.ParsedJson = jsonOutput;
                return View("Index");
            }

            try
            {
                var newEvent = new Event
                {
                    Title = jsonOutput["title"]?.ToString(),
                    Description = jsonOutput["description"]?.ToString(),
                    StartDate = eventDateTime,
                    EndDate = eventDateTime.AddHours(1),
                    UserId = user.Id
                };

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                ViewBag.Success = "Etkinlik başarıyla kaydedildi.";
                ViewBag.ParsedJson = jsonOutput;
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Veritabanına kayıt başarısız: " + ex.Message;
                ViewBag.ParsedJson = jsonOutput;
            }

            return View("Index");
        }
    }
}
