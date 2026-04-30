using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.ViewModel
{
    public class DisableTwoFactorViewModel
    {
        [Required]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }
    }
}
