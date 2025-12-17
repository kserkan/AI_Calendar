using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCalendar.Seed;
using SmartCalendar.Models;
using System.Text;
using System.Text.Json;
using System.Security.Claims;

namespace SmartCalendar.Controllers
{
    public class AIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AIController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public class AiQueryModel { public string Query { get; set; } }

        // --- 1. ETKİNLİK ÖNERİSİ ---
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetSuggestion([FromBody] AiQueryModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Query)) return BadRequest(new { success = false, message = "Sorgu boş." });

            var apiKey = _configuration["Gemini:ApiKey"];
            var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var historyData = await GetHistoryText(userId, 50);

            var systemPrompt = $@"
            Sen 'Calendar AI' adında akıllı bir takvim asistanısın. Şu an: {DateTime.Now:yyyy-MM-dd HH:mm dddd}.
            
            GÖREV: Kullanıcı isteğini geçmiş verilere göre analiz et ve en uygun planı oluştur.
            
            KURALLAR:
            1. Tarih/Saat belirtilmediyse geçmiş alışkanlıklara (Day/Time pattern) bakarak tahmin et.
            2. 'analysisNote' alanına, neden bu tarihi veya saati seçtiğini nazikçe açıkla.
            3. Karar vermende etkili olan 1-3 eski etkinliği 'referenceEvents' listesine ekle.

            GEÇMİŞ VERİLER:
            {historyData}

            JSON ÇIKTISI:
            {{
                ""title"": ""Başlık"", ""description"": ""Açıklama"", ""location"": ""Konum"",
                ""startDate"": ""YYYY-MM-DDTHH:mm:ss"", ""durationMinutes"": 60,
                ""analysisNote"": ""Analiz nedeni..."",
                ""referenceEvents"": [ ""Örn: Halı Saha (Salı 22:00)"" ]
            }}";

            return await CallGemini(geminiUrl, systemPrompt, model.Query);
        }

        // --- 2. HAFTALIK ANALİZ ---
        [HttpPost]
        [Route("AI/GetWeeklyAnalysis")]
        public async Task<IActionResult> GetWeeklyAnalysis()
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var historyData = await GetHistoryText(userId, 100);

            var systemPrompt = $@"
            Sen 'Calendar AI' Yaşam Koçusun. Geçmiş etkinlikleri incele.

            VERİ:
            {historyData}

            JSON ÇIKTISI:
            {{
                ""summary"": ""Genel özet..."",
                ""busyDays"": [""Pazartesi"", ""Cuma""],
                ""habits"": [""Sporlarını akşam yapıyorsun"", ""Sabahları toplantın oluyor""],
                ""suggestion"": ""Koç tavsiyesi.""
            }}";

            return await CallGemini(geminiUrl, systemPrompt, "Haftalık analizimi yap", true);
        }

        // --- YARDIMCI METOTLAR ---
        private async Task<string> GetHistoryText(string userId, int count)
        {
            if (string.IsNullOrEmpty(userId)) return "";
            var events = await _context.Events.Where(e => e.UserId == userId).OrderByDescending(e => e.StartDate).Take(count)
                .Select(e => new { e.Title, Day = e.StartDate.DayOfWeek.ToString(), Time = e.StartDate.ToString("HH:mm"), Date = e.StartDate.ToString("dd.MM.yyyy") }).ToListAsync();
            var sb = new StringBuilder();
            foreach (var e in events) sb.AppendLine($"- {e.Title} ({e.Day} {e.Time}) [{e.Date}]");
            return sb.ToString();
        }

        private async Task<IActionResult> CallGemini(string url, string sysPrompt, string userPrompt, bool isAnalysis = false)
        {
            var requestBody = new { system_instruction = new { parts = new[] { new { text = sysPrompt } } }, contents = new[] { new { parts = new[] { new { text = userPrompt } } } }, generationConfig = new { responseMimeType = "application/json" } };
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode) return BadRequest(new { success = false, message = "AI Servis Hatası" });
                var text = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                var data = JsonSerializer.Deserialize<object>(text);
                return Json(new { success = true, suggestion = isAnalysis ? null : data, analysis = isAnalysis ? data : null });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }
    }
}