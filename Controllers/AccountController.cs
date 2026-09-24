using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccessEase12.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccessEaseDbContext _db;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public AccountController(
            AccessEaseDbContext db,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        // ============================================================
        // LOGIN - GET
        // ============================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectUserByRole(User);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ============================================================
        // LOGIN - POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();

            var user = await _db.AppUsers
                .FirstOrDefaultAsync(x => x.Email == email);

            // User does not exist
            if (user == null)
            {
                await SaveLoginLogAsync(
                    null,
                    email,
                    "Failed");

                ModelState.AddModelError(
                    "",
                    "Invalid email or password.");

                return View(model);
            }

            // Verify password
            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                await SaveLoginLogAsync(
                    user,
                    email,
                    "Failed");

                ModelState.AddModelError(
                    "",
                    "Invalid email or password.");

                return View(model);
            }

            // Sign in
            await SignInUser(user);

            await SaveLoginLogAsync(
                user,
                email,
                "Success");

            // ========================================================
            // RETURN URL
            // ========================================================

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                // Project Manager should not be sent into
                // developer-only scanning pages.
                if (user.Role == "ProjectManager" &&
                    returnUrl.Contains(
                        "/Home/Scan",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Home");
                }

                return Redirect(returnUrl);
            }

            // ========================================================
            // ROLE-BASED REDIRECT
            // ========================================================

            return RedirectUserByRole(user.Role);
        }

        // ============================================================
        // REGISTER - GET
        // ============================================================
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(
                    "Dashboard",
                    "Home");
            }

            return View();
        }

        // ============================================================
        // REGISTER - POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();

            // Check whether email already exists
            var emailExists = await _db.AppUsers
                .AnyAsync(x => x.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already registered.");

                return View(model);
            }

            // ========================================================
            // AUTOMATIC ROLE ASSIGNMENT
            // ========================================================

            string role;

            if (email.Equals(
                "admin@accessease.com",
                StringComparison.OrdinalIgnoreCase))
            {
                role = "Admin";
            }
            else if (email.Equals(
                "pm@accessease.com",
                StringComparison.OrdinalIgnoreCase))
            {
                role = "ProjectManager";
            }
            else
            {
                role = "Developer";
            }

            // ========================================================
            // CREATE USER
            // ========================================================

            var user = new AppUser
            {
                FullName = model.FullName.Trim(),
                Email = email,
                PhoneNumber = null,
                Role = role
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    model.Password);

            _db.AppUsers.Add(user);

            await _db.SaveChangesAsync();

            // Automatically log the user in
            await SignInUser(user);

            // Send user to correct dashboard
            return RedirectUserByRole(user.Role);
        }

        // ============================================================
        // PROFILE - GET
        // ============================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login");

            if (!int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login");

            var user = await _db.AppUsers
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                return NotFound();

            var vm = new EditProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(vm);
        }

        // ============================================================
        // PROFILE - POST
        // ============================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(
            EditProfileViewModel model)
        {
            var userIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login");

            if (!int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login");

            var user = await _db.AppUsers
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();

            // Check if another user already has this email
            var emailUsedByOther =
                await _db.AppUsers.AnyAsync(x =>
                    x.Email == email &&
                    x.Id != userId);

            if (emailUsedByOther)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already used by another account.");

                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.Email = email;

            user.PhoneNumber =
                string.IsNullOrWhiteSpace(model.PhoneNumber)
                    ? null
                    : model.PhoneNumber.Trim();

            await _db.SaveChangesAsync();

            // Refresh authentication claims
            await RefreshUserClaims(user);

            TempData["Success"] =
                "Profile updated successfully.";

            return RedirectToAction("Profile");
        }

        // ============================================================
        // LOGOUT
        // ============================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }

        // ============================================================
        // CHANGE PASSWORD - GET
        // ============================================================
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // ============================================================
        // CHANGE PASSWORD - POST
        // ============================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login");

            if (!int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login");

            var user = await _db.AppUsers
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
                return NotFound();

            // Verify current password
            var verifyResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.CurrentPassword);

            if (verifyResult ==
                PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "CurrentPassword",
                    "Current password is incorrect.");

                return View(model);
            }

            // Prevent same password
            if (model.CurrentPassword ==
                model.NewPassword)
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "New password cannot be the same as the current password.");

                return View(model);
            }

            // Hash new password
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    model.NewPassword);

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Password changed successfully.";

            return RedirectToAction(
                "ChangePassword");
        }

        // ============================================================
        // USER MANAGEMENT
        // PROJECT MANAGER ONLY
        // ============================================================
        [Authorize(Roles = "ProjectManager")]
        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            var users = await _db.AppUsers
                .OrderBy(u => u.FullName)
                .Select(u => new UserManagementViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return View(users);
        }

        // ============================================================
        // UPDATE USER ROLE
        // PROJECT MANAGER ONLY
        // ============================================================
        [Authorize(Roles = "ProjectManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(
            int id,
            string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                TempData["Error"] =
                    "Role cannot be empty.";

                return RedirectToAction(
                    "UserManagement");
            }

            // Allowed roles
            var allowedRoles = new[]
            {
                "Developer",
                "ProjectManager",
                "Admin"
            };

            if (!allowedRoles.Contains(role))
            {
                TempData["Error"] =
                    "Invalid role selected.";

                return RedirectToAction(
                    "UserManagement");
            }

            // Get current logged-in user
            var currentUserIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(
                currentUserIdClaim))
            {
                return RedirectToAction("Login");
            }

            if (!int.TryParse(
                currentUserIdClaim,
                out int currentUserId))
            {
                return RedirectToAction("Login");
            }

            // Find target user
            var user = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                TempData["Error"] =
                    "User not found.";

                return RedirectToAction(
                    "UserManagement");
            }

            // Prevent changing own role
            if (user.Id == currentUserId)
            {
                TempData["Error"] =
                    "You cannot change your own role.";

                return RedirectToAction(
                    "UserManagement");
            }

            user.Role = role;

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "User role updated successfully.";

            return RedirectToAction(
                "UserManagement");
        }

        // ============================================================
        // SIGN IN USER
        // ============================================================
        private async Task SignInUser(AppUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.FullName ?? ""),

                new Claim(
                    ClaimTypes.Email,
                    user.Email ?? ""),

                new Claim(
                    ClaimTypes.Role,
                    user.Role ?? "Developer")
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);
        }

        // ============================================================
        // REFRESH USER CLAIMS
        // ============================================================
        private async Task RefreshUserClaims(
            AppUser user)
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            await SignInUser(user);
        }

        // ============================================================
        // ROLE-BASED REDIRECT USING CLAIMS
        // ============================================================
        private IActionResult RedirectUserByRole(
            ClaimsPrincipal user)
        {
            if (user.IsInRole("Admin"))
            {
                return RedirectToAction(
                    "Dashboard",
                    "Home");
            }

            if (user.IsInRole("ProjectManager"))
            {
                return RedirectToAction(
                    "Dashboard",
                    "Home");
            }

            if (user.IsInRole("Developer"))
            {
                return RedirectToAction(
                    "Scan",
                    "Home");
            }

            return RedirectToAction(
                "Login",
                "Account");
        }

        // ============================================================
        // ROLE-BASED REDIRECT USING DATABASE ROLE
        // ============================================================
        private IActionResult RedirectUserByRole(
            string? role)
        {
            switch (role)
            {
                case "Admin":
                    return RedirectToAction(
                        "Dashboard",
                        "Home");

                case "ProjectManager":
                    return RedirectToAction(
                        "Dashboard",
                        "Home");

                case "Developer":
                    return RedirectToAction(
                        "Scan",
                        "Home");

                default:
                    return RedirectToAction(
                        "Login",
                        "Account");
            }
        }

        // ============================================================
        // SAVE LOGIN LOG
        // ============================================================
        private async Task SaveLoginLogAsync(
            AppUser? user,
            string email,
            string status)
        {
            var ipAddress =
                HttpContext?.Connection?.RemoteIpAddress?
                .ToString() ?? "";

            var log = new LoginLog
            {
                AppUserId = user?.Id,
                FullName = user?.FullName ?? "",
                Email = user?.Email ?? email,
                Role = user?.Role ?? "",
                Status = status,
                LoginTime = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            _db.LoginLogs.Add(log);

            await _db.SaveChangesAsync();
        }
    }
}