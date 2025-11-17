using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SmartCalendar.Models; // bu satırın başta olduğuna emin ol!



public class HolidayService
{
    private readonly HttpClient _httpClient;

    public HolidayService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<HolidayItem>> GetHolidayItemsAsync(int year, string countryCode = "TR")
    {
        var url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var holidayList = new List<HolidayItem>();

        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var date = item.TryGetProperty("date", out var dateProp) ? DateTime.Parse(dateProp.GetString()) : DateTime.MinValue;
            var localName = item.TryGetProperty("localName", out var localNameProp) ? localNameProp.GetString() : "Bilinmeyen";
            var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unknown";
            var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "Unknown";

            if (date != DateTime.MinValue)
            {
                holidayList.Add(new HolidayItem
                {
                    Date = date,
                    LocalName = localName,
                    Name = name,
                    Type = type
                });
            }
        }

        return holidayList;
    }
}
