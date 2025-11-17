using Microsoft.AspNetCore.Mvc;
using SmartCalendar.Services;
using System.Threading.Tasks;

namespace SmartCalendar.Controllers
{
    public class WeatherController : Controller
    {
        private readonly WeatherService _weatherService;

        public WeatherController(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWeather(string city = "Konya")
        {
            var weather = await _weatherService.GetWeatherAsync(city);
            ViewBag.Weather = weather;
            ViewBag.City = city;
            return PartialView("~/Views/Shared/_WeatherPartial.cshtml");
        }
    }
}
