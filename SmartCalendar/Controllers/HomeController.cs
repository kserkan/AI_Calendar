using Microsoft.AspNetCore.Mvc;
using SmartCalendar.Models;
using SmartCalendar.Models.Weather;
using SmartCalendar.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly HolidayService _holidayService;
    private readonly WeatherService _weatherService;

    public HomeController(HolidayService holidayService, WeatherService weatherService)
    {
        _holidayService = holidayService;
        _weatherService = weatherService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!User.Identity.IsAuthenticated)
            return RedirectToAction("Login", "Account");

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CalculateDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
        {
            ViewBag.Error = "Başlangıç tarihi, bitiş tarihinden sonra olamaz!";
            return PartialView("_DateResultPartial");
        }

        var totalDays = (endDate - startDate).TotalDays;
        var totalWeeks = Math.Floor(totalDays / 7);
        var totalHours = (endDate - startDate).TotalHours;

        var allHolidays = new List<HolidayItem>();
        for (int year = startDate.Year; year <= endDate.Year; year++)
            allHolidays.AddRange(await _holidayService.GetHolidayItemsAsync(year));

        var rangeHolidays = allHolidays
            .Where(h => h.Date >= startDate && h.Date <= endDate)
            .ToList();

        var religious = rangeHolidays
            .Where(h => h.LocalName.Contains("Bayram") || h.Name.Contains("Eid")).ToList();

        var national = rangeHolidays.Except(religious).ToList();

        int businessDays = 0;
        foreach (var date in EachDay(startDate, endDate))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
                !rangeHolidays.Any(h => h.Date == date))
            {
                businessDays++;
            }
        }

        ViewBag.TotalDays = totalDays;
        ViewBag.TotalWeeks = totalWeeks;
        ViewBag.TotalHours = totalHours;
        ViewBag.BusinessDays = businessDays;
        ViewBag.ReligiousHolidays = religious;
        ViewBag.NationalHolidays = national;

        return PartialView("_DateResultPartial");
    }

    [HttpGet]
    public async Task<IActionResult> GetWeatherPartial(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            ViewBag.Weather = null;
            return PartialView("_WeatherPartial");
        }

        var weather = await _weatherService.GetWeatherAsync(city);
        ViewBag.Weather = weather;
        ViewBag.City = city;

        return PartialView("_WeatherPartial");
    }


    private static IEnumerable<DateTime> EachDay(DateTime from, DateTime thru)
    {
        for (var day = from.Date; day <= thru.Date; day = day.AddDays(1))
            yield return day;
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }
}
