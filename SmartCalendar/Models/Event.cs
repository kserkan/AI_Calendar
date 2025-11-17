using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartCalendar.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public int? ReminderMinutesBefore { get; set; } // Dakika cinsinden
        public bool ReminderSent { get; set; } = false;

        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? GoogleEventId { get; set; }

        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
        
    }
}
