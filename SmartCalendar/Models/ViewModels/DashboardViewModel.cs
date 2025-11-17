using SmartCalendar.Models;
using System.Collections.Generic;

namespace SmartCalendar.ViewModels
{
    public class DashboardViewModel
    {
        public List<Event> TodayEvents { get; set; } = new();
        public List<Event> TomorrowEvents { get; set; } = new();
        public List<Event> ThisWeekEvents { get; set; } = new();

        // Ekstra istatistikler
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int PastEvents { get; set; }
    }

}
