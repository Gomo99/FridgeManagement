using Google.Authenticator;
using System.Security.Cryptography;
using System.Text;

namespace FridgeManagement.Service
{
    public class TwoFactorAuthService
    {
        private readonly TwoFactorAuthenticator _twoFactorAuthenticator;

        public TwoFactorAuthService()
        {
            _twoFactorAuthenticator = new TwoFactorAuthenticator();
        }

        public string GenerateSecretKey()
        {
            var key = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return Convert.ToBase32String(key);
        }

        public SetupCode GenerateQrCode(string email, string secretKey, string appName = "FridgeManagement")
        {
            return _twoFactorAuthenticator.GenerateSetupCode(appName, email, secretKey, false);
        }

        public bool ValidatePin(string secretKey, string pin)
        {
            return _twoFactorAuthenticator.ValidateTwoFactorPIN(secretKey, pin);
        }

        public List<string> GenerateRecoveryCodes(int count = 10)
        {
            var recoveryCodes = new List<string>();
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < count; i++)
                {
                    var bytes = new byte[4];
                    rng.GetBytes(bytes);
                    var code = Convert.ToBase32String(bytes).Replace("=", "");
                    recoveryCodes.Add(code);
                }
            }
            return recoveryCodes;
        }
    }

    // Helper class for Base32 encoding
    public static class Convert
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string ToBase32String(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var result = new StringBuilder();
            int buffer = bytes[0];
            int next = 1;
            int bitsLeft = 8;

            while (bitsLeft > 0 || next < bytes.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < bytes.Length)
                    {
                        buffer <<= 8;
                        buffer |= bytes[next++] & 0xFF;
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = 0x1F & (buffer >> (bitsLeft - 5));
                bitsLeft -= 5;
                result.Append(Base32Alphabet[index]);
            }

            return result.ToString();
        }
    }
}
