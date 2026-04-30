using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.ViewModel
{
    public class VerificationCodeLoginViewModel
    {
        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }

        public bool RememberThisDevice { get; set; }

    }
}
