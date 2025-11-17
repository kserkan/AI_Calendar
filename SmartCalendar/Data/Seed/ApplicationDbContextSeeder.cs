using Microsoft.EntityFrameworkCore;
using SmartCalendar.Models; // Event, Tag, User modelleriniz burada olmalı
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Program.cs'deki çağrıyla eşleşen ad alanı (namespace)
namespace SmartCalendar.Seed
{
    // Program.cs'deki çağrıyla eşleşen sınıf adı
    public static class ApplicationDbContextSeeder
    {
        // Program.cs'deki çağrıyla eşleşen metod adı
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // --- TEST KULLANICISI ---
            // 'serkan@example.com' e-postalı kullanıcıyı veritabanından bul
            var targetEmail = "serkan@example.com";
            var testUser = await context.Users.FirstOrDefaultAsync(u => u.Email == targetEmail);

            // ÖNEMLİ: Kullanıcı bulunamazsa, hata vermeden işlemi durdur.
            // Lütfen 'serkan@example.com' kullanıcısının kayıtlı olduğundan emin olun.
            if (testUser == null)
            {
                // İsteğe bağlı olarak buraya bir loglama ekleyebilirsiniz:
                // Console.WriteLine($"Seeder: {targetEmail} kullanıcısı bulunamadı. Veri eklenmedi.");
                return;
            }

            // Kullanıcının ID'sini (GUID) al
            var testUserId = testUser.Id;

            // --- 1. ETİKETLERİ OLUŞTUR ---
            // Etiketlerin tekrar eklenmesini önlemek için kontrol
            var tagIs = await context.Tags.FirstOrDefaultAsync(t => t.Name == "İş") ?? new Tag { Name = "İş" };
            var tagToplanti = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Toplantı") ?? new Tag { Name = "Toplantı" };
            var tagKisisel = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Kişisel") ?? new Tag { Name = "Kişisel" };
            var tagSpor = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Spor") ?? new Tag { Name = "Spor" };
            var tagEgitim = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Eğitim") ?? new Tag { Name = "Eğitim" };
            var tagTatil = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Tatil") ?? new Tag { Name = "Tatil" };

            // Sadece veritabanında olmayan etiketleri ekle
            if (!await context.Tags.AnyAsync(t => t.Name == "İş")) context.Tags.Add(tagIs);
            if (!await context.Tags.AnyAsync(t => t.Name == "Toplantı")) context.Tags.Add(tagToplanti);
            if (!await context.Tags.AnyAsync(t => t.Name == "Kişisel")) context.Tags.Add(tagKisisel);
            if (!await context.Tags.AnyAsync(t => t.Name == "Spor")) context.Tags.Add(tagSpor);
            if (!await context.Tags.AnyAsync(t => t.Name == "Eğitim")) context.Tags.Add(tagEgitim);
            if (!await context.Tags.AnyAsync(t => t.Name == "Tatil")) context.Tags.Add(tagTatil);

            await context.SaveChangesAsync(); // Etiketleri kaydet

            // --- 2. SENTETİK ETKİNLİK VERİLERİ ---
            var rand = new Random();
            var eventsToSeed = new List<Event>();

            var titles = new[] {
                "Proje Sunumu", "Müşteri Toplantısı", "Ekip Toplantısı", "Dişçi Randevusu",
                "Spor Salonu", "Ders Çalışma", "Akşam Yemeği", "Pazar Alışverişi", "Flutter Projesi"
            };
            var locations = new[] { "Ofis", "Ev (Online)", "Spor Salonu", "Kampüs", "Restoran", "Dışarıda" };

            // 60 gün geçmişe ve 10 gün geleceğe veri ekleyelim
            for (int i = -60; i < 10; i++)
            {
                // Her gün %70 ihtimalle etkinlik oluştur
                if (rand.Next(0, 10) < 7)
                {
                    var title = titles[rand.Next(titles.Length)];
                    var location = locations[rand.Next(locations.Length)];

                    var startDate = DateTime.Now.Date.AddDays(i)
                                        .AddHours(rand.Next(8, 18))
                                        .AddMinutes(rand.Next(0, 2) * 30);

                    var newEvent = new Event
                    {
                        Title = title,
                        StartDate = startDate,
                        EndDate = startDate.AddHours(rand.Next(1, 3)),
                        Description = "Bu, 'ApplicationDbContextSeeder' tarafından sentetik olarak oluşturulmuş bir etkinliktir.",
                        Location = location,
                        UserId = testUserId, // <-- Değişiklik burada
                        ReminderSent = false,
                        Tags = new List<Tag>()
                    };

                    // Veriyi daha anlamlı hale getirelim (AI için)
                    if (title.Contains("Toplantı") || title.Contains("Sunum"))
                        newEvent.Tags.Add(tagToplanti);
                    if (title.Contains("Toplantı") || title.Contains("Sunum") || title.Contains("Proje"))
                        newEvent.Tags.Add(tagIs);
                    if (title.Contains("Spor"))
                        newEvent.Tags.Add(tagSpor);
                    if (title.Contains("Ders") || title.Contains("Proje"))
                        newEvent.Tags.Add(tagEgitim);
                    if (title.Contains("Dişçi") || title.Contains("Yemek") || title.Contains("Alışveriş"))
                        newEvent.Tags.Add(tagKisisel);

                    if (!newEvent.Tags.Any())
                        newEvent.Tags.Add(tagKisisel);

                    eventsToSeed.Add(newEvent);
                }
            }

            await context.Events.AddRangeAsync(eventsToSeed);
            await context.SaveChangesAsync();
        }
    }
}