using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartCalendar.Models;
using SmartCalendar.Models.Dtos;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public ProfileController(UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var model = new ProfileUpdateDto
        {
            FullName = user.FullName,
            Email = user.Email
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(ProfileUpdateDto model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // Eğer şifre değişikliği isteniyorsa ama sadece bir alan doldurulmuşsa hata ver
        if (!string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            if (string.IsNullOrWhiteSpace(model.NewPassword) || string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                ModelState.AddModelError(string.Empty, "Şifre değiştirmek için hem mevcut hem yeni şifreyi girin.");
                return View("Index", model);
            }

            var passwordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View("Index", model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirildi.";
            return RedirectToAction("Index");
        }

        // Profil bilgileri güncelleniyorsa
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Lütfen zorunlu alanları doldurun.");
            return View("Index", model);
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email; // Email'i aynı zamanda kullanıcı adı olarak kullanıyorsan

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View("Index", model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Profil başarıyla güncellendi!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEmailSettings(bool? ReceiveReminders)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.ReceiveReminders = ReceiveReminders ?? false;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "E-posta ayarınız güncellendi.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> EmailSettings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        return View(user);
    }


    // ============================================================
    // 📱 MOBİL API METODLARI (YENİ EKLENDİ)
    // ============================================================

    // API: GET /Profile/Api/GetProfile?userId=...
    [AllowAnonymous]
    [HttpGet("Api/GetProfile")]
    public async Task<IActionResult> ApiGetProfile(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "User ID gerekli." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Json(new { success = false, message = "Kullanıcı bulunamadı." });

        return Json(new
        {
            success = true,
            user = new
            {
                fullName = user.FullName,
                email = user.Email,
                receiveReminders = user.ReceiveReminders
            }
        });
    }

    // API: POST /Profile/Api/UpdateProfile
    // Mobilden isim güncelleme vs. için kullanılabilir
    [AllowAnonymous]
    [HttpPost("Api/UpdateProfile")]
    public async Task<IActionResult> ApiUpdateProfile([FromBody] MobileProfileUpdateDto model)
    {
        if (string.IsNullOrEmpty(model.UserId))
            return Json(new { success = false, message = "User ID gerekli." });

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
            return Json(new { success = false, message = "Kullanıcı bulunamadı." });

        user.FullName = model.FullName;
        // İstersen email güncellemesini de buraya ekleyebilirsin
        // user.Email = model.Email; 

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return Json(new { success = true, message = "Profil güncellendi." });
        }

        return Json(new { success = false, message = "Güncelleme başarısız." });
    }
}

// Mobilden gelen veri için basit bir DTO sınıfı (Dosyanın en altına veya Dtos klasörüne ekleyebilirsin)
public class MobileProfileUpdateDto
{
    public string UserId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
}


