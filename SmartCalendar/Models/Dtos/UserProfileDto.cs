namespace SmartCalendar.Models.Dtos
{
    public class UserProfileDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public bool ReceiveReminders { get; set; }
        // Add this property
    }
}
