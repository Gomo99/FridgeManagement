using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.ViewModel
{
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class DisableTwoFactorViewModel
    {
        [Required]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }
    }


    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email or Username")]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }



    public class TwoFactorLoginViewModel
    {
        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }

        public bool UseRecoveryCode { get; set; } = false;

        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; }
    }


    public class TwoFactorSetupViewModel
    {
        public string QrCodeImageUrl { get; set; }
        public string ManualEntryKey { get; set; }
        public string VerificationCode { get; set; }
        public List<string> RecoveryCodes { get; set; } = new List<string>();



    }


    public class VerificationCodeLoginViewModel
    {
        [Required]
        [Display(Name = "Verification Code")]
        public string VerificationCode { get; set; }

        public bool RememberThisDevice { get; set; }

    }


    public class ViewProfileViewModel
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public string iTitle { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string UserStatus { get; set; }
        public string Title { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
    }
}
