// DOSYA: AIController.cs
// (Mevcut dosyanın içeriğini bununla değiştirin)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using SmartCalendar;
using SmartCalendar.Models;
using SmartCalendar.Services;
using SmartCalendar.Models.Dtos;
using System.Text.Json; // <-- YENİ (AIService'den JsonElement almak için)

namespace SmartCalendar.Controllers
{
    // === DTO (Veri Taşıma Nesneleri) ===
    public class PromptRequest
    {
        public string Prompt { get; set; }
    }
    public class RecommendationEventModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string SuggestedDate { get; set; }
        public string SuggestedTime { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public double DurationHours { get; set; }
    }
    public class WeeklyAnalysisPatterns
    {
        public int TotalEvents { get; set; }
        public string FavoriteCategory { get; set; }
    }

    // === AI KÖPRÜ KONTROLÖRÜ ===

    [Authorize]
    [Route("ai")]
    public class AIController : Controller
    {
        private readonly AIService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AIController> _logger;

        public AIController(
            AIService aiService,
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<AIController> logger)
        {
            _aiService = aiService;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// (DÜZELTİLDİ) Index.cshtml'nin beklediği AI önerilerini yükler.
        /// Artık AIService'den gelen tam JSON'u doğrudan arayüze iletir.
        /// </summary>
        [HttpGet("smart-recommendations")]
        public async Task<IActionResult> SmartRecommendations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var events = await _context.Events
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.StartDate)
                .Take(10)
                .Include(e => e.Tags)
                .ToListAsync();

            var eventDtos = events.Select(e => new Models.EventDto
            {
                Day = e.StartDate.ToString("dddd", new CultureInfo("tr-TR")),
                Time = e.StartDate.ToString("HH:mm"),
                Title = e.Title,
                Tags = e.Tags?.Select(t => t.Name).ToList() ?? new List<string>(),
                Location = e.Location ?? string.Empty
            }).ToList();

            // DEĞİŞİKLİK: AIService'den artık 'string' değil, 'JsonElement' geliyor
            var aiJsonResponse = await _aiService.GetEventRecommendationAsync(eventDtos);

            // DEĞİŞİKLİK: "success = false" zorlaması kaldırıldı.
            // Python'dan (app.py) gelen JSON (başarılı veya hatalı)
            // doğrudan Index.cshtml'e (JavaScript) iletiliyor.
            return Json(aiJsonResponse);
        }

        /// <summary>
        /// (DÜZELTİLDİ) AI öneri kartındaki "Takvime Ekle" butonu için.
        /// </summary>
        [HttpPost("create-from-recommendation")]
        public async Task<IActionResult> CreateFromRecommendation([FromBody] RecommendationEventModel model)
        {
            _logger.LogInformation("📥 Öneriden etkinlik oluşturuluyor: {Title}", model.Title);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!DateTime.TryParse($"{model.SuggestedDate} {model.SuggestedTime}", out var startDate))
            {
                _logger.LogError("❌ Geçersiz tarih formatı: {Date} {Time}", model.SuggestedDate, model.SuggestedTime);
                return BadRequest(new { success = false, message = "AI geçerli bir tarih döndürmedi." });
            }

            try
            {
                var newEvent = new Event
                {
                    Title = model.Title,
                    Description = model.Description,
                    StartDate = startDate,
                    EndDate = startDate.AddHours(model.DurationHours > 0 ? model.DurationHours : 1),
                    UserId = userId,
                    Location = model.Location ?? "",
                    ReminderMinutesBefore = 10,
                    ReminderSent = false
                };

                var tagName = model.Category;
                if (!string.IsNullOrWhiteSpace(tagName))
                {
                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (tag == null)
                    {
                        tag = new Tag { Name = tagName };
                        _context.Tags.Add(tag);
                    }
                    newEvent.Tags = new List<Tag> { tag };
                }

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Öneriden etkinlik başarıyla oluşturuldu: {Title}", newEvent.Title);
                return Ok(new { success = true, message = "Etkinlik oluşturuldu.", eventId = newEvent.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Öneriden etkinlik oluşturulurken hata oluştu.");
                return StatusCode(500, new { success = false, message = "Etkinlik oluşturulurken sunucu hatası." });
            }
        }

        /// <summary>
        /// (YENİ) Index.cshtml'deki "Doğal Dil ile Hızlı Ekleme" kutusu için.
        /// </summary>
        [HttpPost("parse-and-create")]
        public async Task<IActionResult> ParseAndCreate([FromBody] PromptRequest req)
        {
            _logger.LogInformation("📥 Doğal dil ile hızlı ekleme: {Prompt}", req.Prompt);

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonConvert.SerializeObject(new { prompt = req.Prompt }),
                Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync("http://localhost:5001/api/parse-event", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Flask servisine (parse-event) bağlanılamadı.");
                return StatusCode(500, new { success = false, message = "AI servisine bağlanılamadı." });
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Flask /api/parse-event hatası: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return BadRequest(new { success = false, message = "AI'dan geçerli JSON alınamadı." });
            }

            var result = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("📤 Flask JSON (parse-event): {Result}", result);

            dynamic parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject(result);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "❌ JSON parse hatası.");
                return BadRequest(new { success = false, message = "AI JSON formatı geçersiz." });
            }

            dynamic eventData = parsed?.parsed;
            if (eventData == null || string.IsNullOrWhiteSpace((string)eventData.title))
                return BadRequest(new { success = false, message = "AI geçerli bir etkinlik döndürmedi." });

            if (!DateTime.TryParse($"{eventData.date} {eventData.time}", out var startDate))
                return BadRequest(new { success = false, message = "AI geçerli bir tarih döndürmedi." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var newEvent = new Event
                {
                    Title = eventData.title,
                    Description = eventData.description,
                    StartDate = startDate,
                    EndDate = startDate.AddHours(1),
                    UserId = userId,
                    Location = eventData.location,
                    ReminderMinutesBefore = 10,
                    ReminderSent = false
                };

                var tagName = (string)eventData.category;
                if (!string.IsNullOrWhiteSpace(tagName))
                {
                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (tag == null)
                    {
                        tag = new Tag { Name = tagName };
                        _context.Tags.Add(tag);
                    }
                    newEvent.Tags = new List<Tag> { tag };
                }

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Hızlı ekleme ile etkinlik oluşturuldu: {Title}", newEvent.Title);
                return Ok(new { success = true, message = "Etkinlik oluşturuldu.", eventId = newEvent.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Hızlı ekleme etkinliği oluşturulurken hata oluştu.");
                return StatusCode(500, new { success = false, message = "Etkinlik veritabanına kaydedilirken hata oluştu." });
            }
        }


        /// <summary>
        /// (YENİ) Index.cshtml'deki "Haftalık Analiz" butonu için.
        /// </summary>
        [HttpGet("weekly-analysis")]
        public async Task<IActionResult> GetWeeklyAnalysis()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7);

            var events = await _context.Events
                .Where(e => e.UserId == userId && e.StartDate >= startOfWeek && e.StartDate < endOfWeek)
                .Include(e => e.Tags)
                .OrderBy(e => e.StartDate)
                .ToListAsync();

            if (!events.Any())
            {
                return Json(new { success = true, analysis = "Bu hafta için planlanmış bir etkinliğiniz bulunmuyor. AI analizi için lütfen etkinlik ekleyin.", patterns = (object)null });
            }

            var eventSummary = string.Join("\n", events.Select(e =>
                $"- {e.StartDate:dddd HH:mm}: {e.Title} (Kategori: {e.Tags.FirstOrDefault()?.Name ?? "Genel"})"
            ));

            var prompt = $@"
Kullanıcının bu haftaki etkinlikleri aşağıdadır.
Bu verilere dayanarak kısa, samimi bir haftalık analiz yap ve 2-3 cümlelik bir verimlilik ipucu ver.
JSON veya markdown KULLANMA. Sadece düz metin olarak cevap ver.

Etkinlikler:
{eventSummary}

Analiz:
";

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonConvert.SerializeObject(new { prompt = prompt }),
                Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync("http://localhost:5001/api/chat", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Flask servisine (weekly-analysis) bağlanılamadı.");
                return StatusCode(500, new { success = false, message = "AI servisine bağlanılamadı." });
            }

            var result = await response.Content.ReadAsStringAsync();

            dynamic parsed;
            string aiAnalysis = "Analiz alınamadı.";
            try
            {
                parsed = JsonConvert.DeserializeObject(result);
                aiAnalysis = parsed?.response ?? "AI'dan metin alınamadı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık analiz JSON'u parse edilemedi.");
            }

            var patterns = new WeeklyAnalysisPatterns
            {
                TotalEvents = events.Count,
                FavoriteCategory = events.SelectMany(e => e.Tags)
                                        .GroupBy(t => t.Name)
                                        .OrderByDescending(g => g.Count())
                                        .FirstOrDefault()?.Key ?? "Genel"
            };

            return Json(new { success = true, analysis = aiAnalysis, patterns = patterns });
        }
    }
}