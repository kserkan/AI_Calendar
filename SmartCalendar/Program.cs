using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.DataProtection; // <-- BU EKLENDİ (Login sorunu için şart)
using System.Text;
using SmartCalendar.Services;
using SmartCalendar.Models; // User modelinin olduğu yer
using SmartCalendar.Seed;   // ApplicationDbContext'in olduğu yer

var builder = WebApplication.CreateBuilder(args);

// --- 1. DATA PROTECTION (GİRİŞ SORUNU ÇÖZÜMÜ) ---
// Bu blok, şifreleme anahtarlarını sabit bir klasöre kaydeder.
// Bunu yapmazsak Docker her yeniden başladığında giriş yapan herkesi atar.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("SmartCalendarApp");

// --- 2. SERVISLER VE AYARLAR ---

builder.Services.AddHttpClient<HolidayService>();

// CORS Politikası (Flutter ve Web Erişimi İçin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFlutter", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Veritabanı Bağlantısı (MySQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// JWT Ayarları (Mobil API İçin)
var jwtSettings = builder.Configuration.GetSection("Jwt");
// Key boş gelirse hata vermemesi için varsayılan bir key (Güvenlik için appsettings.json dolu olmalı)
var keyStr = jwtSettings["Key"] ?? "Bu_Cok_Gizli_Ve_Uzun_Bir_Anahtar_Olmali_123456789";
var key = Encoding.UTF8.GetBytes(keyStr);

// Identity Kurulumu
builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Authentication (Cookie + JWT + Google)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Web öncelikli
})
.AddCookie(options =>
{
    // Web Cookie Ayarları (HTTP Uyumlu ve Kalıcı Giriş)
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14); // 14 gün giriş açık kalsın
    options.SlidingExpiration = true;

    // --- KRİTİK DÜZELTME (HTTP ERİŞİMİ İÇİN) ---
    // HTTP üzerinden giriş yapabilmek için Lax ve SameAsRequest ayarı şarttır.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // ------------------------------------------
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Mobil JWT Ayarları
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["GoogleAuth:ClientId"];
    options.ClientSecret = builder.Configuration["GoogleAuth:ClientSecret"];
    options.SaveTokens = true;
});

// Cookie Policy (HTTP için ek güvenlik ayarı)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax; // HTTP için Lax olmalı
    options.CheckConsentNeeded = context => false; // GDPR onayı gerekmeden çalışsın
});

// Arkaplan Servisleri ve Diğerleri
builder.Services.AddHostedService<ReminderService>();
builder.Services.AddScoped<SmtpEmailService>();
builder.Services.AddHttpContextAccessor();

// Controller ve JSON Döngü Ayarı
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Harici Servisler
builder.Services.AddHttpClient<WeatherService>();
builder.Services.AddHttpClient<AIService>();

// Hata Davranışları
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName);
});

var app = builder.Build();

// --- 3. UYGULAMA BAŞLATMA (MIDDLEWARE) ---

// Otomatik Veritabanı Oluşturma (Auto-Migration)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate(); // Veritabanı yoksa oluşturur
        Console.WriteLine("--> Veritabanı migration islemi basarili.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> Veritabanı hatasi: {ex.Message}");
    }
}

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartCalendar API v1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowFlutter"); // Flutter izni
app.UseCookiePolicy(); // Cookie politikası middleware'i

app.UseAuthentication(); // Önce kimlik doğrulama
app.UseAuthorization();  // Sonra yetkilendirme

// Rotalar
app.MapControllerRoute(
    name: "account",
    pattern: "Account/{action}/{id?}",
    defaults: new { controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});

app.MapControllers();

app.Run();