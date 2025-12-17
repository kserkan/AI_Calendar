namespace SmartCalendar.Models.Dtos
{
    public class EventDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public int ReminderMinutesBefore { get; set; }

        public List<int>? TagIds { get; set; }
        public string UserId { get; set; }
    }

}
