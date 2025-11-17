using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using SmartCalendar.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class GoogleCalendarService
{
    private readonly string _apiKey;

    public GoogleCalendarService(IConfiguration configuration)
    {
        _apiKey = configuration["GoogleCalendar:ApiKey"];
    }

    public async Task<List<HolidayItem>> GetHolidaysFromGoogleAsync(DateTime startDate, DateTime endDate)
    {
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            ApiKey = _apiKey,
            ApplicationName = "SmartCalendar"
        });

        // Türkiye tatil takvimi (public calendar)
        var request = service.Events.List("tr.turkish#holiday@group.v.calendar.google.com");
        request.TimeMin = startDate;
        request.TimeMax = endDate;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var result = await request.ExecuteAsync();
        var holidays = new List<HolidayItem>();

        if (result.Items != null)
        {
            foreach (var ev in result.Items)
            {
                holidays.Add(new HolidayItem
                {
                    Name = ev.Summary,
                    LocalName = ev.Summary,
                    Date = DateTime.Parse(ev.Start.Date)
                });
            }
        }

        return holidays;
    }
}
