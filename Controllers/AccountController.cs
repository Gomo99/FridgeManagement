using FridgeManagement.AppStatus;
using FridgeManagement.Data;
using FridgeManagement.Models;
using FridgeManagement.Service;
using FridgeManagement.ViewModel;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FridgeManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly TwoFactorAuthService _twoFactorService;
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private const string TwoFactorRememberCookie = "SP.2FA.Remember";
        private static readonly TimeSpan RememberDuration = TimeSpan.FromDays(30);

        public AccountController(ApplicationDbContext context, EmailService _emailservice, TwoFactorAuthService twoFactorAuthService)
        {
            _context = context;
            _emailService = _emailservice;
            _twoFactorService = twoFactorAuthService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // Find user by username (case-insensitive)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower() && u.Status == Status.Active);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            // Plain text password comparison
            if (user.PasswordHash != model.Password)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            // Create claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.GivenName, user.Username),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("UserID", user.Id.ToString())

    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // --- ROLE-BASED REDIRECTION ---
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Redirect to the appropriate dashboard based on role
            return RedirectToDashboard(user.Role);
        }

        // Helper method for role-based redirection
        private IActionResult RedirectToDashboard(UserRole role)
        {
            return role switch
            {
                UserRole.ADMINISTRATOR => RedirectToAction("DashBoard", "Admin"),
                UserRole.CUSTOMERLIAISON => RedirectToAction("Dashboard", "CustomerLiaisonManagement"),
                UserRole.INVENTORYLIAISON => RedirectToAction("Dashboard", "InventoryLiaisonManagement"),
                UserRole.CUSTOMER => RedirectToAction("Dashboard", "Customer"),
                UserRole.FAULTTECHNICIAN => RedirectToAction("Dashboard", "FaultTechnician"),
                UserRole.MAINTENANCETECHNICIAN => RedirectToAction("Dashboard", "Maintenance"),
                UserRole.PURCHASINGMANAGER => RedirectToAction("Dashboard", "Purchasing"),
                UserRole.SUPPLIER => RedirectToAction("Dashboard", "Supplier"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }




        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Please enter your email address.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                string resetPin = new Random().Next(100000, 999999).ToString();
                var placeholders = new Dictionary<string, string>
        {
            { "Name", user.Name },
            { "Email", email },
            { "ResetPin", resetPin }
        };

                try
                {
                    var subject = "Password Reset Request - Fridge Management";
                    await _emailService.SendEmailWithTemplateAsync(email, subject, "password-reset-template.html", placeholders);

                    user.ResetPin = resetPin;
                    user.ResetPinExpiration = DateTime.Now.AddMinutes(5);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "A password reset PIN has been sent to your email address.";
                    return View("ResetPassword");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while sending the reset email. Please try again.";
                    return View();
                }
            }

            TempData["ErrorMessage"] = "The email address you entered is not associated with any account.";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string pin, string newPassword)
        {
            if (string.IsNullOrEmpty(pin) || pin.Length != 6 || !pin.All(char.IsDigit))
            {
                TempData["ErrorMessage"] = "PIN must be a 6-digit code.";
                return View();
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                TempData["ErrorMessage"] = "Please enter a new password.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(a =>
                a.ResetPin == pin &&
                a.ResetPinExpiration > DateTime.Now);

            if (user != null)
            {
                // 🔐 Hash the new password with bcrypt
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                user.ResetPin = null;
                user.ResetPinExpiration = null;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password reset successfully. You can now login with your new password.";
                return RedirectToAction("Login");
            }

            TempData["ErrorMessage"] = "Invalid PIN or PIN has expired.";
            return View();
        }



        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ViewProfile()
        {
            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Changed to async
            var user = await _context.Users.FirstOrDefaultAsync(a => a.Id == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var model = new ViewProfileViewModel
            {
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Title = user.Title,
                Role = user.Role.ToString(),
                UserStatus = user.Status.ToString(),
                IsTwoFactorEnabled = user.IsTwoFactorEnabled
            };

            return View(model);
        }



        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var userIdClaim = User.FindFirst("UserID");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Login");
                }

                var user = await _context.Users.FirstOrDefaultAsync(a => a.Id == userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Login");
                }

                // ✅ Only allow bcrypt hashed passwords
                if (!user.PasswordHash.StartsWith("$2a$") &&
                    !user.PasswordHash.StartsWith("$2b$") &&
                    !user.PasswordHash.StartsWith("$2y$"))
                {
                    TempData["ErrorMessage"] = "Your account is using an outdated password format. Please reset your password.";
                    return RedirectToAction("ForgotPassword");
                }

                // 🔐 Verify current password with bcrypt
                if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return View(model);
                }

                if (model.CurrentPassword == model.NewPassword)
                {
                    TempData["ErrorMessage"] = "The new password cannot be the same as the current password.";
                    return View(model);
                }

                if (model.NewPassword != model.ConfirmPassword)
                {
                    TempData["ErrorMessage"] = "The new password and confirmation password do not match.";
                    return View(model);
                }

                // ✅ Always hash new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                await _context.SaveChangesAsync();

                var placeholders = new Dictionary<string, string>
        {
            { "Name", user.Name },
            { "ChangeDate", DateTime.Now.ToString("dddd, MMMM dd, yyyy hh:mm tt") }
        };

                await _emailService.SendEmailWithTemplateAsync(
                    user.Email,
                    "Security Alert: Your Password Was Changed",
                    "PasswordChangeTemplate.html",
                    placeholders
                );

                TempData["SuccessMessage"] = "Password changed successfully!";
                await RevokeAllRememberedDevicesAsync(user.Id);

                return user.Role switch
                {
                   UserRole.ADMINISTRATOR => RedirectToAction("DashBoard", "AdminController", new { area = "" }),
                UserRole.CUSTOMERLIAISON => RedirectToAction("DashBoard", "CustomerLiaisonManagementController", new { area = "" }),
                UserRole.INVENTORYLIAISON => RedirectToAction("DashBoard", "InventoryLiaisonManagementController", new { area = "" }),
                UserRole.CUSTOMER => RedirectToAction("DashBoard", "CustomerController", new { area = "" }),
                UserRole.FAULTTECHNICIAN => RedirectToAction("DashBoard", "FaultTechnicianController", new { area = "" }),
                UserRole.MAINTENANCETECHNICIAN => RedirectToAction("DashBoard", "MaintenanceController", new { area = "" }),
                UserRole.PURCHASINGMANAGER => RedirectToAction("DashBoard", "PurchasingController", new { area = "" }),
                UserRole.SUPPLIER => RedirectToAction("DashBoard", "SupplierController", new { area = "" }),
                    _ => RedirectToAction("Index", "Home"),
                };
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while changing your password. Please try again.";
                return View(model);
            }
        }


        [Authorize]
        [HttpGet]
        public IActionResult DeleteAccount()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccountConfirmed()
        {
            var userName = User.Identity.Name;
            // Changed to async
            var user = await _context.Users.FirstOrDefaultAsync(a => a.Username == userName);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            user.Status = Status.Inactive;
            await _context.SaveChangesAsync();
            await HttpContext.SignOutAsync("MyCookieAuth");
            TempData["SuccessMessage"] = "Your account has been deactivated successfully.";
            return RedirectToAction("Login", "Account");
        }





        [Authorize(Roles = "Administrator")]
        [HttpGet]
        public async Task<IActionResult> ReactivateAccount()
        {
            // Changed to async
            var users = await _context.Users
                .Where(u => u.Status == Status.Inactive)
                .ToListAsync();

            return View(users);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateAccount(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ReactivateAccount");
            }

            user.Status = Status.Active;
            _context.Update(user);
            await _context.SaveChangesAsync();
                
            TempData["SuccessMessage"] = $"User {user.Name} has been reactivated successfully.";
            return RedirectToAction("ReactivateAccount");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> DownloadProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(a => a.Id == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var memoryStream = new MemoryStream();

            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new iText.Layout.Document(pdf);

            document.Add(new iText.Layout.Element.Paragraph("User Profile")
                .SetFontSize(18));

            document.Add(new iText.Layout.Element.Paragraph($"Name: {user.Name}"));
            document.Add(new iText.Layout.Element.Paragraph($"Surname: {user.Surname}"));
            document.Add(new iText.Layout.Element.Paragraph($"Email: {user.Email}"));
            document.Add(new iText.Layout.Element.Paragraph($"Role: {user.Role}"));
            document.Add(new iText.Layout.Element.Paragraph($"Status: {user.Status}"));

            document.Close();

            memoryStream.Position = 0;

            return File(memoryStream, "application/pdf", "Profile.pdf");
        }




        [HttpGet]
        public IActionResult VerificationCodeLogin()
        {
            var userId = HttpContext.Session.GetInt32("TwoFactorUserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            return View(new VerificationCodeLoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificationCodeLogin(VerificationCodeLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = HttpContext.Session.GetInt32("TwoFactorUserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Check if account is locked
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = $"Account is locked until {user.LockoutEnd.Value.ToLocalTime():hh:mm tt}.";
                return View(model);
            }

            bool isValidCode = _twoFactorService.ValidatePin(user.TwoFactorSecretKey, model.VerificationCode);

            if (isValidCode)
            {
                // Reset failed attempts after successful 2FA
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await _context.SaveChangesAsync();

                if (model.RememberThisDevice)
                {
                    var deviceName = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await RememberCurrentDeviceAsync(user, deviceName);
                }

                HttpContext.Session.Remove("TwoFactorUserId");
                await SignInUser(user, false);

                TempData["SuccessMessage"] = "Two-factor authentication successful!";
                return RedirectBasedOnRole(user.Role);
            }

            // Increment failed attempts for invalid 2FA code
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                TempData["ErrorMessage"] = $"Too many failed attempts. Account locked for {LockoutDuration.TotalMinutes} minutes.";
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid verification code.";
            }
            await _context.SaveChangesAsync();

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> SetupTwoFactor()
        {
            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Changed to async
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            if (user.IsTwoFactorEnabled)
            {
                TempData["ErrorMessage"] = "Two-factor authentication is already enabled.";
                return RedirectToAction("ViewProfile");
            }

            var secretKey = _twoFactorService.GenerateSecretKey();
            var setupCode = _twoFactorService.GenerateQrCode(user.Email, secretKey);

            var viewModel = new TwoFactorSetupViewModel
            {
                QrCodeImageUrl = setupCode.QrCodeSetupImageUrl,
                ManualEntryKey = secretKey
            };

            HttpContext.Session.SetString("TempSecretKey", secretKey);
            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupTwoFactor(TwoFactorSetupViewModel model)
        {
            if (string.IsNullOrEmpty(model.VerificationCode))
            {
                TempData["ErrorMessage"] = "Please enter the verification code from your authenticator app.";
                return View(model);
            }

            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var secretKey = HttpContext.Session.GetString("TempSecretKey");
            if (string.IsNullOrEmpty(secretKey))
            {
                TempData["ErrorMessage"] = "Session expired. Please try again.";
                return RedirectToAction("SetupTwoFactor");
            }

            if (!_twoFactorService.ValidatePin(secretKey, model.VerificationCode))
            {
                TempData["ErrorMessage"] = "Invalid verification code. Please try again.";
                var setupCode = _twoFactorService.GenerateQrCode(user.Email, secretKey);
                model.QrCodeImageUrl = setupCode.QrCodeSetupImageUrl;
                model.ManualEntryKey = secretKey;
                return View(model);
            }

            var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();
            user.IsTwoFactorEnabled = true;
            user.TwoFactorSecretKey = secretKey;
            user.TwoFactorRecoveryCodes = string.Join(",", recoveryCodes);

            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("TempSecretKey");

            model.RecoveryCodes = recoveryCodes;
            TempData["SuccessMessage"] = "Two-factor authentication has been enabled successfully!";
            return View("TwoFactorRecoveryCodes", model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Changed to async
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || !user.IsTwoFactorEnabled)
            {
                TempData["ErrorMessage"] = "Two-factor authentication is not enabled.";
                return RedirectToAction("ViewProfile");
            }

            return View(new DisableTwoFactorViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userIdClaim = User.FindFirst("UserID");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            if (user.PasswordHash != model.CurrentPassword)
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return View(model);
            }

            if (!_twoFactorService.ValidatePin(user.TwoFactorSecretKey, model.VerificationCode))
            {
                TempData["ErrorMessage"] = "Invalid verification code.";
                return View(model);
            }

            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecretKey = null;
            user.TwoFactorRecoveryCodes = null;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Two-factor authentication has been disabled successfully.";
            await RevokeAllRememberedDevicesAsync(user.Id);
            return RedirectToAction("ViewProfile");
        }


        [Authorize]
        public async Task<IActionResult> ManageDevices()
        {
            var userIdClaim = User.FindFirst("UserID")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim);

            var devices = await _context.RememberedDevices
                .Where(d => d.UserId == userId && !d.Revoked)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return View(devices);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeDevice(int id)
        {
            var userIdClaim = User.FindFirst("UserID")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim);
            var device = await _context.RememberedDevices
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (device == null)
            {
                TempData["ErrorMessage"] = "Device not found.";
                return RedirectToAction(nameof(ViewProfile));
            }

            device.Revoked = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Device revoked successfully.";
            return RedirectToAction(nameof(ViewProfile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAllDevices()
        {
            var userIdClaim = User.FindFirst("UserID")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            var userId = int.Parse(userIdClaim);

            var devices = await _context.RememberedDevices
                .Where(d => d.UserId == userId && !d.Revoked)
                .ToListAsync();

            foreach (var d in devices)
                d.Revoked = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "All remembered devices have been revoked successfully.";
            return RedirectToAction(nameof(ViewProfile));
        }



        [Authorize]
        [HttpGet]
        public IActionResult ResendRecoveryCodes()
        {
            // Ensure the 2FA session is active
            var userId = HttpContext.Session.GetInt32("TwoFactorUserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRecoveryCodesPost()
        {
            var userId = HttpContext.Session.GetInt32("TwoFactorUserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            if (!user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                TempData["ErrorMessage"] = "Two-factor authentication is not enabled for this account.";
                return RedirectToAction("ViewProfile");
            }

            // Generate new recovery codes
            var recoveryCodes = _twoFactorService.GenerateRecoveryCodes();

            // Save codes in the database
            user.TwoFactorRecoveryCodes = string.Join(",", recoveryCodes);
            await _context.SaveChangesAsync();

            // Prepare email placeholders
            var placeholders = new Dictionary<string, string>
    {
        { "Name", user.Name },
        { "RecoveryCodes", string.Join("<br>", recoveryCodes) }, // HTML line breaks
        { "Date", DateTime.Now.ToString("f") }
    };

            try
            {
                await _emailService.SendEmailWithTemplateAsync(
                    user.Email,
                    "Your New Two-Factor Recovery Codes",
                    "RecoveryCodesTemplate.html", // create this HTML template
                    placeholders
                );

                TempData["SuccessMessage"] = "New recovery codes have been generated and sent to your email. Please check your inbox.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to send recovery codes via email: {ex.Message}";
                return View();
            }

            // Optionally show them on the screen too
            return View("TwoFactorRecoveryCodes", new TwoFactorSetupViewModel { RecoveryCodes = recoveryCodes });
        }




        private async Task RevokeAllRememberedDevicesAsync(int userId)
        {
            var items = _context.RememberedDevices.Where(r => r.UserId == userId);
            _context.RememberedDevices.RemoveRange(items);
            await _context.SaveChangesAsync();

            // Delete cookie on this browser (other browsers still hold their own cookies)
            Response.Cookies.Delete(TwoFactorRememberCookie);
        }





        private IActionResult RedirectBasedOnRole(UserRole role)
        {
            // (Same as above – used in other places like ChangePassword)
            return role switch
            {
                UserRole.ADMINISTRATOR => RedirectToAction("DashBoard", "Admin"),
                UserRole.CUSTOMERLIAISON => RedirectToAction("Dashboard", "CustomerLiaisonManagement"),
                UserRole.INVENTORYLIAISON => RedirectToAction("Dashboard", "InventoryLiaisonManagement"),
                UserRole.CUSTOMER => RedirectToAction("Dashboard", "Customer"),
                UserRole.FAULTTECHNICIAN => RedirectToAction("Dashboard", "FaultTechnician"),
                UserRole.MAINTENANCETECHNICIAN => RedirectToAction("Dashboard", "Maintenance"),
                UserRole.PURCHASINGMANAGER => RedirectToAction("Dashboard", "Purchasing"),
                UserRole.SUPPLIER => RedirectToAction("Dashboard", "Supplier"),
            };
        }



        private async Task RememberCurrentDeviceAsync(User user, string? deviceName = null)
        {
            var rawToken = Guid.NewGuid().ToString("N");
            var tokenHash = HashToken(rawToken);

            var record = new RememberedDevice
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.Add(RememberDuration),
                DeviceName = deviceName,
                UserAgent = Request.Headers["User-Agent"].ToString()
            };

            _context.RememberedDevices.Add(record);
            await _context.SaveChangesAsync();

            Response.Cookies.Append(
                TwoFactorRememberCookie,
                rawToken,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.Add(RememberDuration),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
        }


        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha.ComputeHash(bytes);
            return System.Convert.ToBase64String(hash);
        }




        private async Task SignInUser(User user, bool isPersistent)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, $"{user.Name} {user.Surname}"),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("UserID", user.Id.ToString())
    };

            var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent
            };

            await HttpContext.SignInAsync(
                "MyCookieAuth",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }



    }
}