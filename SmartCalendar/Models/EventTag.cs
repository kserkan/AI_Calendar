namespace SmartCalendar.Models
{
    public class EventTag
    {
        public int EventsId { get; set; }
        public Event Events { get; set; }

        public int TagsId { get; set; }
        public Tag Tag { get; set; }
    }

}
