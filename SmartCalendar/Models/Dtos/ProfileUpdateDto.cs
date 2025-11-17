using System.ComponentModel.DataAnnotations;

namespace SmartCalendar.Models.Dtos
{
    public class ProfileUpdateDto
    {
        [Required]
        public string FullName { get; set; }
        
        [Required, EmailAddress]
        public string Email { get; set; }

        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
