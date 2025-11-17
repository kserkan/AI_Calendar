using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

public class User : IdentityUser
{
    [Required]
    public string FullName { get; set; }
    public bool ReceiveReminders { get; set; }

}
