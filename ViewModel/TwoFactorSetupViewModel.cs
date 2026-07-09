using System.ComponentModel.DataAnnotations;

namespace FridgeManagement.ViewModel
{
    public class TwoFactorSetupViewModel
    {
        public string QrCodeImageUrl { get; set; }
        public string ManualEntryKey { get; set; }
        public string VerificationCode { get; set; }
        public List<string> RecoveryCodes { get; set; } = new List<string>();
    }
}