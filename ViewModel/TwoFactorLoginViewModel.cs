using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.ViewModel
{
    public class TwoFactorLoginViewModel
    {
        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }

        public bool UseRecoveryCode { get; set; } = false;

        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; }
    }
}