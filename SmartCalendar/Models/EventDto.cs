using System;

namespace SmartCalendar.Models
{
    public class EventDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string Day { get; set; }
        public string Time { get; set; }

        public List<string> Tags { get; set; }
        public string Location { get; set; }
    }
}
