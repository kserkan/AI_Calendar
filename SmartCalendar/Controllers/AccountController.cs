using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SmartCalendar.Models;
using SmartCalendar.Models.Dtos;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("Account")]
public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration; // 👈 _configuration class seviyesinde tanımlandı

    public AccountController(UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration; // 👈 _configuration burada başlatılıyor!
    }

    // GET: /Account/Register
    [HttpGet("Register")]
    public IActionResult Register() => View();

    // POST: /Account/Register
    [HttpPost("Register")]
    public async Task<IActionResult> Register(UserRegisterDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new User { UserName = model.Email, Email = model.Email, FullName = model.FullName };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: /Account/Login
    [HttpGet("Login")]
    public IActionResult Login() => View();

    // POST: /Account/Login
    [HttpPost("Login")]
    public async Task<IActionResult> Login(UserLoginDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Geçersiz giriş bilgileri!");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return Redirect(returnUrl ?? "/Calendar");
        }

        ModelState.AddModelError(string.Empty, "Geçersiz giriş bilgileri!");
        return View(model);
    }

    // API: POST /Account/Api/Login
    [AllowAnonymous]
    [HttpPost("Api/Login")]
    public async Task<IActionResult> ApiLogin([FromBody] UserLoginDto model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Geçersiz veri" });
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Json(new { success = false, message = "Geçersiz giriş bilgileri!" });
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            // JWT token oluştur (basit implementation)
            var token = Guid.NewGuid().ToString(); // Gerçek JWT token implementasyonu eklenebilir
            
            return Json(new { 
                success = true, 
                message = "Giriş başarılı",
                token = token,
                user = new {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName
                }
            });
        }

        return Json(new { success = false, message = "Geçersiz giriş bilgileri!" });
    }

    // API: POST /Account/Api/Register
    [AllowAnonymous]
    [HttpPost("Api/Register")]
    public async Task<IActionResult> ApiRegister([FromBody] UserRegisterDto model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Geçersiz veri" });
        }

        var user = new User { UserName = model.Email, Email = model.Email, FullName = model.FullName };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            return Json(new { success = true, message = "Kayıt başarılı" });
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return Json(new { success = false, message = errors });
    }

    // 🔹 Google Login Callback

    [HttpGet("signin-google")]
    public async Task<IActionResult> GoogleResponse()
    {
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

        if (!result.Succeeded)
        {
            TempData["Error"] = "Google kimlik doğrulaması başarısız.";
            return RedirectToAction("Login");
        }

        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                FullName = name
            };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return RedirectToAction("Login");
        }

        // ✅ Google'dan alınan tokenları al
        var tokens = result.Properties.GetTokens();
        var props = new AuthenticationProperties();
        props.StoreTokens(tokens); // access_token ve id_token'ı sakla

        // 🔑 Kullanıcı girişini token’larla birlikte yap
        await _signInManager.SignInAsync(user, props, CookieAuthenticationDefaults.AuthenticationScheme);

        // 🧹 External oturumu temizle
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return RedirectToAction("Index", "Calendar");
    }





    // 🔐 Google Login Başlat
    [HttpGet("GoogleLogin")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action("GoogleResponse", "Account", null, Request.Scheme);
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }


    // 🔓 Logout (Hem Google hem Identity için)
    [HttpGet("Logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }


}
