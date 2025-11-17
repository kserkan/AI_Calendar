using Newtonsoft.Json.Linq;
using SmartCalendar.Models;
using SmartCalendar.Models.Weather;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartCalendar.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "43d519211f8ba6d7776804010d78900d"; // Buraya kendi API anahtarınızı girin

        public WeatherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            try
            {
                var url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&units=metric&lang=tr&appid={_apiKey}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());

                return new WeatherInfo
                {
                    City = json["name"]?.ToString(),
                    Description = json["weather"]?[0]?["description"]?.ToString(),
                    Temperature = Math.Round(json["main"]?["temp"]?.Value<double>() ?? 0, 1),
                    WindSpeed = json["wind"]?["speed"]?.Value<double>() ?? 0,
                    Humidity = json["main"]?["humidity"]?.Value<int>() ?? 0,
                    Date = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
