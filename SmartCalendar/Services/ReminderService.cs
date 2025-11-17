using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartCalendar.Models;
using System.Net.Mail;

public class ReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReminderService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var emailService = scope.ServiceProvider.GetRequiredService<SmtpEmailService>();

            var now = DateTime.Now;

            var events = await context.Events
                .Where(e => !e.ReminderSent &&
                            e.ReminderMinutesBefore != null &&
                            e.StartDate.AddMinutes(-e.ReminderMinutesBefore.Value) <= now)
                .Include(e => e.User)
                .ToListAsync();

            Console.WriteLine("[REMINDER CHECK] " + DateTime.Now);
            Console.WriteLine("[REMINDER] " + events.Count + " adet etkinlik bulundu.");

            foreach (var ev in events)
            {
                var user = await userManager.FindByIdAsync(ev.UserId);
                if (user == null)
                {
                    Console.WriteLine("[REMINDER SKIPPED] Kullanıcı bulunamadı.");
                    continue;
                }

                if (!user.ReceiveReminders)
                {
                    Console.WriteLine($"[REMINDER SKIPPED] {user.Email} hatırlatma e-postası almak istemiyor.");
                    continue;
                }

                string body = $@"
                <div style='font-family:Segoe UI, sans-serif; max-width:600px; margin:auto; border:1px solid #ddd; padding:20px; border-radius:10px; background:#f9f9f9;'>
                  <h2 style='color:#007bff;'>📅 SmartCalendar Hatırlatıcısı</h2>
                  <p>Merhaba {user.UserName},</p>

                  <div style='background:#fff; padding:15px; border:1px solid #ccc; border-radius:5px;'>
                    <p><strong>🔔 Etkinlik:</strong> {ev.Title}</p>
                    <p><strong>🕒 Tarih:</strong> {ev.StartDate:dddd, dd MMMM yyyy HH:mm}</p>
                    {(ev.EndDate != null ? $"<p><strong>🛑 Bitiş:</strong> {ev.EndDate:dddd, dd MMMM yyyy HH:mm}</p>" : "")}
                    {(ev.Description != null ? $"<p><strong>📝 Açıklama:</strong> {ev.Description}</p>" : "")}
                    {(ev.Location != null ? $"<p><strong>📍 Konum:</strong> {ev.Location}</p>" : "")}
                  </div>

                  <p style='margin-top:20px;'>📌 Bu e-posta {ev.ReminderMinutesBefore} dakika önceden gönderildi.</p>

                  <a href='https://localhost:5001/Calendar/Details/{ev.Id}' style='display:inline-block; margin-top:15px; padding:10px 20px; background:#28a745; color:#fff; text-decoration:none; border-radius:5px;'>📆 Etkinliği Görüntüle</a>

                  <p style='margin-top:30px; font-size:0.9em; color:#999;'>SmartCalendar ile planlı günler dileriz!</p>
                </div>";

                await emailService.SendAsync(
                    user.Email,
                    "📅 Yaklaşan Etkinlik: " + ev.Title,
                    body
                );

                Console.WriteLine($"[REMINDER SENT] {ev.Title} - {user.Email}");
                ev.ReminderSent = true;
            }

            await context.SaveChangesAsync();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}