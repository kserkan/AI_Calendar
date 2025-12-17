using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCalendar.Models;
using SmartCalendar.Models.Dtos;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Event = SmartCalendar.Models.Event;
using Microsoft.AspNetCore.Authentication.Google;
using SmartCalendar.Models.ViewModels;
using Newtonsoft.Json;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public class DeleteEventRequest
{
    public int Id { get; set; }
}

[Authorize]
[Route("[controller]")]
public class CalendarController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly SmtpEmailService _emailService;


    public CalendarController(ApplicationDbContext context, UserManager<User> userManager, IHttpContextAccessor httpContextAccessor, SmtpEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _emailService = emailService;


    }

    [HttpGet("GetGoogleHolidayEvents")]
    public async Task<IActionResult> GetGoogleHolidayEvents()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { message = "Google erişim token'ı alınamadı." });

        var credential = GoogleCredential.FromAccessToken(token);
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SmartCalendar"
        });

        // Türkiye Resmi Tatil Takvimi
        var request = service.Events.List("tr.turkish#holiday@group.v.calendar.google.com");
        request.TimeMin = DateTime.UtcNow;
        request.TimeMax = DateTime.UtcNow.AddYears(1); // 1 yıl ileriye kadar al
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var holidays = await request.ExecuteAsync();

        var events = holidays.Items.Select(e => new
        {
            title = $"🎌 {e.Summary}",
            start = e.Start.Date,
            color = e.Summary.Contains("Bayram") ? "#ffc107" : "#28a745"
        });

        return Json(events);
    }


    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string tag, int page = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int pageSize = 5; // her sayfada 5 etkinlik

        // 👇 BU LOGLARI EKLE (AJANLAR)
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine("🌐 WEB TAKVİMİ AÇILDI!");
        Console.WriteLine($"👤 Giriş Yapan Web Kullanıcısı ID: '{userId}'");
        Console.WriteLine("--------------------------------------------------");


        var eventsQuery = _context.Events
            .Include(e => e.Tags)
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrEmpty(tag))
        {
            eventsQuery = eventsQuery.Where(e => e.Tags.Any(t => t.Name == tag));
        }

        var totalEvents = await eventsQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalEvents / pageSize);

        var pagedEvents = await eventsQuery
            .OrderByDescending(e => e.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.PagedEvents = pagedEvents;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.Tags = await _context.Tags.ToListAsync();

        return View(pagedEvents); // aynı zamanda Model olarak da tüm sayfadaki eventleri gönderiyoruz (kullanılmak istenirse)
    }






    [HttpPost("DeleteEvent")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventToDelete = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventToDelete == null)
            return Json(new { success = false, message = "Etkinlik bulunamadı" });

        // ✅ Google’dan sil
        if (!string.IsNullOrEmpty(eventToDelete.GoogleEventId))
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                var credential = GoogleCredential.FromAccessToken(token);
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "SmartCalendar"
                });

                try
                {
                    await service.Events.Delete("primary", eventToDelete.GoogleEventId).ExecuteAsync();
                    Console.WriteLine("✅ Google etkinliği silindi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Google etkinliği silinemedi: " + ex.Message);
                }
            }
        }

        // 🔥 Veritabanından sil
        _context.Events.Remove(eventToDelete);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }



    // 📥 Tüm Etkinlikleri Getir (Lokal)
    [HttpGet("GetEvents")]
    public async Task<IActionResult> GetEvents(string tag)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var eventsQuery = _context.Events
            .Include(e => e.Tags)
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrEmpty(tag))
        {
            eventsQuery = eventsQuery.Where(e => e.Tags.Any(t => t.Name.ToLower() == tag.ToLower()));
        }

        var events = await eventsQuery
            .Select(e => new
            {
                id = e.Id.ToString(),
                title = e.Title,
                start = e.StartDate,
                end = e.EndDate,
                description = e.Description,
                location = e.Location,
                tags = e.Tags.Select(t => t.Name).ToList(),
                isCompleted = e.EndDate.HasValue && e.EndDate.Value < DateTime.Now
            })
            .ToListAsync();

        return Json(events);
    }

    // API: GET /Calendar/Api/GetEvents
    [AllowAnonymous]
    [HttpGet("Api/GetEvents")]
    public async Task<IActionResult> ApiGetEvents(string userId, string tag) // 👈 Parametreye userId ekledik
    {
        try
        {
            // 🛑 HATALI KOD SİLİNDİ: var userId = "bbc1...";

            // ✅ DÜZELTME: Parametre olarak gelen userId'ye göre filtrele
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { error = "UserId parametresi zorunludur." });
            }

            var eventsQuery = _context.Events
                .Include(e => e.Tags)
                .Where(e => e.UserId == userId); // 🔥 Gelen ID'ye göre ara

            if (!string.IsNullOrEmpty(tag))
            {
                eventsQuery = eventsQuery.Where(e => e.Tags.Any(t => t.Name.ToLower() == tag.ToLower()));
            }

            var events = await eventsQuery
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    start = e.StartDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    description = e.Description,
                    location = e.Location,
                    tags = e.Tags.Select(t => t.Name).ToList(),
                    isCompleted = e.EndDate.HasValue && e.EndDate.Value < DateTime.Now
                })
                .ToListAsync();

            return Json(events);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    // API: POST /Calendar/Api/AddEvent
    // API: POST /Calendar/Api/AddEvent
    [AllowAnonymous]
    [HttpPost("Api/AddEvent")]
    public async Task<IActionResult> ApiAddEvent([FromBody] SmartCalendar.Models.Dtos.EventDto model)
    {
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"🔍 EKLEME İSTEĞİ GELDİ!");
        Console.WriteLine($"📦 Gelen Başlık: {model.Title}");

        // DİKKAT: Mobilden "UserId" isminde geliyor, DTO'da bu property olmalı.
        // Eğer model.UserId hata veriyorsa, EventDto sınıfına 'public string UserId { get; set; }' ekle.
        Console.WriteLine($"👤 Gelen UserId: '{model.UserId}'");

        try
        {
            // 🛑 SİLİNEN SATIR: var userId = "bbc1..."; (ARTIK BU YOK!)

            // Mobilden gelen ID'yi kontrol et
            if (string.IsNullOrEmpty(model.UserId) || model.UserId == "0")
            {
                return Json(new { success = false, message = "UserId boş veya hatalı geldi!" });
            }

            var newEvent = new Event
            {
                Title = model.Title,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Description = model.Description,
                Location = model.Location,
                ReminderMinutesBefore = model.ReminderMinutesBefore,
                ReminderSent = false,
                UserId = model.UserId, // 🔥 ARTIK SABİT DEĞİL, MOBİLDEN GELEN DEĞER!
                Tags = new List<Tag>()
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Etkinlik başarıyla eklendi", eventId = newEvent.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // API: POST /Calendar/Api/UpdateEvent
    [AllowAnonymous]
    [HttpPost("Api/UpdateEvent")]
    public async Task<IActionResult> ApiUpdateEvent([FromBody] SmartCalendar.Models.Dtos.EventDto model)
    {
        try
        {
            var eventToUpdate = await _context.Events.FirstOrDefaultAsync(e => e.Id == model.Id);

            if (eventToUpdate == null)
                return Json(new { success = false, message = "Etkinlik bulunamadı" });

            eventToUpdate.Title = model.Title;
            eventToUpdate.StartDate = model.StartDate;
            eventToUpdate.EndDate = model.EndDate;
            eventToUpdate.Description = model.Description;
            eventToUpdate.Location = model.Location;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Etkinlik başarıyla güncellendi" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // API: POST /Calendar/Api/DeleteEvent
    [AllowAnonymous]
    [HttpPost("Api/DeleteEvent")]
    public async Task<IActionResult> ApiDeleteEvent([FromBody] DeleteEventRequest request)
    {
        try
        {
            var eventToDelete = await _context.Events.FirstOrDefaultAsync(e => e.Id == request.Id);

            if (eventToDelete == null)
                return Json(new { success = false, message = "Etkinlik bulunamadı" });

            _context.Events.Remove(eventToDelete);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Etkinlik başarıyla silindi" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }




    // 🔁 Etkinlik Güncelle (Lokal)
    [HttpPost("UpdateEvent")]
    public async Task<IActionResult> UpdateEvent(int id, string title, DateTime startDate, DateTime? endDate, string description, string location)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var eventToUpdate = await _context.Events
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventToUpdate == null)
            return Json(new { success = false, message = "Etkinlik bulunamadı" });

        eventToUpdate.Title = title;
        eventToUpdate.StartDate = startDate;
        eventToUpdate.EndDate = endDate;
        eventToUpdate.Description = description;
        eventToUpdate.Location = location;

        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }


    [HttpPost("AddEventFromCalendar")]
    public async Task<IActionResult> AddEventFromCalendar([FromBody] Event model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        var newEvent = new Event
        {
            Title = model.Title,
            StartDate = model.StartDate,
            EndDate = model.StartDate.AddHours(1),
            UserId = userId
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        // 📧 E-posta gönder
        await _emailService.SendAsync(user.Email, "Yeni Etkinlik Oluşturuldu", $@"
        Merhaba {user.UserName},<br><br>
        <b>{newEvent.Title}</b> etkinliğiniz başarıyla oluşturuldu.<br>
        Başlangıç: {newEvent.StartDate:g}<br>
        <br>SmartCalendar ile planlı günler dileriz.
    ");

        return Ok();
    }


    [HttpPost("UpdateEventFromCalendar")]
    public async Task<IActionResult> UpdateEventFromCalendar([FromBody] SmartCalendar.Models.EventDto model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Console.WriteLine($"Kullanıcı ID: {userId}");
        Console.WriteLine($"Etkinlik ID: {model.Id}");

        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == model.Id);


        if (ev == null)
        {
            Console.WriteLine("Etkinlik bulunamadı!");
            return NotFound(new { success = false, message = "Etkinlik bulunamadı" });
        }

        ev.StartDate = model.StartDate;
        ev.EndDate = model.EndDate;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpGet("AllEvents")]
    public async Task<IActionResult> AllEvents(int page = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int pageSize = 10;

        var query = _context.Events
            .Include(e => e.Tags)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartDate);

        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var events = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(events);
    }

    [HttpGet("AllEventsPartial")]
    public async Task<IActionResult> AllEventsPartial(string tag = "", string status = "")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = _context.Events
            .Include(e => e.Tags)
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(e => e.Tags.Any(t => t.Name == tag));

        if (status == "active")
            query = query.Where(e => !e.EndDate.HasValue || e.EndDate > DateTime.Now);
        else if (status == "completed")
            query = query.Where(e => e.EndDate.HasValue && e.EndDate <= DateTime.Now);

        var model = new AllEventsViewModel
        {
            Events = await query.OrderByDescending(e => e.StartDate).ToListAsync(),
            Tags = await _context.Tags.ToListAsync()
        };

        return PartialView("_AllEventsPartial", model);
    }



    [HttpPost("AddEvent")]
    public async Task<IActionResult> AddEvent(string title, DateTime startDate, DateTime? endDate,
                                   int? reminderMinutesBefore, string description, string location,
                                   List<string> TagIds)
    {
        Console.WriteLine("🎯 Gelen TagIds:");
        foreach (var tag in TagIds)
        {
            Console.WriteLine($"  - {tag}");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        var newEvent = new Event
        {
            Title = title,
            StartDate = startDate,
            EndDate = endDate,
            ReminderMinutesBefore = reminderMinutesBefore,
            ReminderSent = false,
            Description = description,
            Location = location,
            UserId = userId,
            Tags = new List<Tag>()
        };

        // 🔖 Etiketleri işle
        foreach (var tagValue in TagIds)
        {
            Tag tag;
            if (int.TryParse(tagValue, out int tagId))
            {
                tag = await _context.Tags.FindAsync(tagId);
            }
            else
            {
                var trimmed = tagValue.Trim();
                tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == trimmed);
                if (tag == null)
                {
                    tag = new Tag { Name = trimmed };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync();
                }
            }

            if (tag != null)
            {
                newEvent.Tags.Add(tag);
            }
        }

        // 📌 Etkinliği önce EF'e ekle
        _context.Events.Add(newEvent);

        // ✅ GOOGLE TAKVİME EKLE
        var token = await HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            var credential = GoogleCredential.FromAccessToken(token);
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "SmartCalendar"
            });

            var googleEvent = new Google.Apis.Calendar.v3.Data.Event
            {
                Summary = title,
                Description = description,
                Location = location,
                Start = new EventDateTime
                {
                    DateTime = startDate,
                    TimeZone = "Europe/Istanbul"
                },
                End = new EventDateTime
                {
                    DateTime = endDate ?? startDate.AddHours(1),
                    TimeZone = "Europe/Istanbul"
                }
            };

            try
            {
                var googleResult = await service.Events.Insert(googleEvent, "primary").ExecuteAsync();
                newEvent.GoogleEventId = googleResult.Id; // ✅ Google ID veritabanına yazılıyor
                Console.WriteLine("✅ Google Calendar'a eklendi. ID: " + googleResult.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Google Calendar'a eklenemedi: " + ex.Message);
            }
        }

        // 📥 Tüm verileri veritabanına kaydet
        await _context.SaveChangesAsync();

        TempData["Success"] = "Etkinlik başarıyla eklendi.";
        return RedirectToAction("Index");
    }



    [HttpGet("GetGoogleEvents")]
    [Authorize(AuthenticationSchemes = GoogleDefaults.AuthenticationScheme)]
    [Obsolete]
    public async Task<IActionResult> GetGoogleEvents()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var credential = GoogleCredential.FromAccessToken(token);
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SmartCalendar"
        });

        var request = service.Events.List("primary");
        request.TimeMin = DateTime.UtcNow.AddMonths(-1);
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();

        var eventList = events.Items.Select(e => new
        {
            id = e.Id,
            title = e.Summary,
            start = e.Start?.DateTime,
            end = e.End?.DateTime
        }).ToList();

        return Json(eventList);
    }

    [HttpPost("ImportGoogleEvents")]
    public async Task<IActionResult> ImportGoogleEvents()
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { message = "Google erişim token'ı alınamadı." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);

        var credential = GoogleCredential.FromAccessToken(token);
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SmartCalendar"
        });

        var request = service.Events.List("primary");
        request.TimeMin = DateTime.UtcNow.AddMonths(-1); // Geçmiş 1 ayı getir
        request.TimeMax = DateTime.UtcNow.AddMonths(6);  // 6 ay ileriye kadar al
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var response = await request.ExecuteAsync();

        int addedCount = 0;

        foreach (var e in response.Items)
        {
            if (string.IsNullOrEmpty(e.Summary) || !e.Start?.DateTime.HasValue == true) continue;

            // Aynı etkinlik daha önce eklenmiş mi kontrol et
            bool exists = await _context.Events.AnyAsync(ev =>
                ev.UserId == userId &&
                ev.Title == e.Summary &&
                ev.StartDate == e.Start.DateTime);

            if (exists) continue;

            var newEvent = new Event
            {
                Title = e.Summary,
                StartDate = e.Start.DateTime.Value,
                EndDate = e.End?.DateTime ?? e.Start.DateTime.Value.AddHours(1),
                Description = e.Description,
                Location = e.Location,
                UserId = userId,
                Tags = new List<Tag>() // Tag yoksa boş geç
            };

            _context.Events.Add(newEvent);
            addedCount++;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"{addedCount} Google etkinliği içe aktarıldı.";
        return RedirectToAction("Index");
    }

  

}
