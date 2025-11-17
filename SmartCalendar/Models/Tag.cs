// Models/Tag.cs
using SmartCalendar.Models;

namespace SmartCalendar.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
        
    }
}
