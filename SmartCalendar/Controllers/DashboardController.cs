using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCalendar;
using SmartCalendar.ViewModels;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartCalendar.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Ana Dashboard sayfası - AI önerileri dahil
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                // Tüm etkinlikleri al
                var allEvents = await _context.Events
                    .Where(e => e.UserId == userId)
                    .Include(e => e.Tags)
                    .ToListAsync();

                // Haftanın başlangıcı ve bitişi (Pazartesi - Pazar)
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                var endOfWeek = startOfWeek.AddDays(6);

                // ViewModel oluştur
                var model = new DashboardViewModel
                {
                    TodayEvents = allEvents
                        .Where(e => e.StartDate.Date == today)
                        .OrderBy(e => e.StartDate)
                        .ToList(),

                    TomorrowEvents = allEvents
                        .Where(e => e.StartDate.Date == tomorrow)
                        .OrderBy(e => e.StartDate)
                        .ToList(),

                    ThisWeekEvents = allEvents
                        .Where(e => e.StartDate.Date >= startOfWeek && e.StartDate.Date <= endOfWeek)
                        .OrderBy(e => e.StartDate)
                        .ToList(),

                    TotalEvents = allEvents.Count,
                    UpcomingEvents = allEvents.Count(e => e.StartDate > DateTime.Now),
                    PastEvents = allEvents.Count(e => e.EndDate < DateTime.Now)
                };

                // Kullanıcı activity bilgilerini ViewBag'e ekle
                ViewBag.HasEvents = allEvents.Any();
                ViewBag.RecentEventsCount = allEvents.Count(e => e.StartDate > DateTime.Now.AddDays(-30));

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard yüklenirken hata oluştu");
                TempData["Error"] = "Dashboard yüklenirken bir hata oluştu.";
                return View(new DashboardViewModel());
            }
        }

        /// <summary>
        /// Dashboard için özet istatistikler (API)
        /// </summary>
        [HttpGet("Dashboard/Stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.Today;

                var allEvents = await _context.Events
                    .Where(e => e.UserId == userId)
                    .ToListAsync();

                var stats = new
                {
                    total = allEvents.Count,
                    today = allEvents.Count(e => e.StartDate.Date == today),
                    thisWeek = allEvents.Count(e => e.StartDate.Date >= today && e.StartDate.Date <= today.AddDays(7)),
                    thisMonth = allEvents.Count(e => e.StartDate.Month == today.Month && e.StartDate.Year == today.Year),
                    upcoming = allEvents.Count(e => e.StartDate > DateTime.Now),
                    completed = allEvents.Count(e => e.EndDate < DateTime.Now)
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler alınırken hata");
                return Json(new { error = "İstatistikler alınamadı" });
            }
        }

        /// <summary>
        /// Kategori bazlı dağılım (Grafik için)
        /// </summary>
        [HttpGet("Dashboard/CategoryDistribution")]
        public async Task<IActionResult> GetCategoryDistribution()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var distribution = await _context.Events
                    .Where(e => e.UserId == userId)
                    .Include(e => e.Tags)
                    .SelectMany(e => e.Tags.Select(t => t.Name))
                    .GroupBy(t => t)
                    .Select(g => new
                    {
                        category = g.Key,
                        count = g.Count()
                    })
                    .OrderByDescending(x => x.count)
                    .Take(10)
                    .ToListAsync();

                return Json(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kategori dağılımı alınırken hata");
                return Json(new { error = "Dağılım alınamadı" });
            }
        }

        /// <summary>
        /// Son aktiviteler timeline'ı
        /// </summary>
        [HttpGet("Dashboard/RecentActivity")]
        public async Task<IActionResult> GetRecentActivity(int take = 10)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var recentEvents = await _context.Events
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.StartDate)
                    .Take(take)
                    .Include(e => e.Tags)
                    .Select(e => new
                    {
                        id = e.Id,
                        title = e.Title,
                        startDate = e.StartDate,
                        tags = e.Tags.Select(t => t.Name).ToList(),
                        isPast = e.EndDate < DateTime.Now,
                        isToday = e.StartDate.Date == DateTime.Today
                    })
                    .ToListAsync();

                return Json(recentEvents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Son aktiviteler alınırken hata");
                return Json(new { error = "Aktiviteler alınamadı" });
            }
        }

        /// <summary>
        /// Kullanıcı üretkenlik özeti
        /// </summary>
        [HttpGet("Dashboard/ProductivitySummary")]
        public async Task<IActionResult> GetProductivitySummary()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var allEvents = await _context.Events
                    .Where(e => e.UserId == userId)
                    .ToListAsync();

                var summary = new
                {
                    thisWeek = new
                    {
                        total = allEvents.Count(e => e.StartDate >= weekStart),
                        completed = allEvents.Count(e => e.EndDate < DateTime.Now && e.StartDate >= weekStart),
                        upcoming = allEvents.Count(e => e.StartDate >= DateTime.Now && e.StartDate >= weekStart)
                    },
                    thisMonth = new
                    {
                        total = allEvents.Count(e => e.StartDate >= monthStart),
                        completed = allEvents.Count(e => e.EndDate < DateTime.Now && e.StartDate >= monthStart),
                        upcoming = allEvents.Count(e => e.StartDate >= DateTime.Now && e.StartDate >= monthStart)
                    },
                    streakDays = CalculateStreakDays(allEvents),
                    averageEventsPerDay = allEvents.Any() ?
                        Math.Round(allEvents.Count / (double)(DateTime.Now - allEvents.Min(e => e.StartDate)).TotalDays, 1) : 0
                };

                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Üretkenlik özeti alınırken hata");
                return Json(new { error = "Özet alınamadı" });
            }
        }

        /// <summary>
        /// Ardışık gün sayısını hesapla
        /// </summary>
        private int CalculateStreakDays(List<SmartCalendar.Models.Event> events)
        {
            if (!events.Any()) return 0;

            var eventDates = events
                .Select(e => e.StartDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            int streak = 0;
            var currentDate = DateTime.Today;

            foreach (var date in eventDates)
            {
                if (date == currentDate || date == currentDate.AddDays(-1))
                {
                    streak++;
                    currentDate = date.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        /// <summary>
        /// Haftalık özet (Grafik için)
        /// </summary>
        [HttpGet("Dashboard/WeeklyChart")]
        public async Task<IActionResult> GetWeeklyChart()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

                var weeklyData = new List<object>();

                for (int i = 0; i < 7; i++)
                {
                    var day = weekStart.AddDays(i);
                    var count = await _context.Events
                        .Where(e => e.UserId == userId && e.StartDate.Date == day)
                        .CountAsync();

                    weeklyData.Add(new
                    {
                        day = day.ToString("dddd", new System.Globalization.CultureInfo("tr-TR")),
                        date = day.ToString("dd/MM"),
                        count = count,
                        isToday = day == today
                    });
                }

                return Json(weeklyData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık grafik verisi alınırken hata");
                return Json(new { error = "Grafik verisi alınamadı" });
            }
        }
    }
}