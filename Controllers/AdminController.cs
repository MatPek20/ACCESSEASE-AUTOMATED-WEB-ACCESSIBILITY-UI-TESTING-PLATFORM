using System.Security.Claims;
using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessEase12.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public AdminController(AccessEaseDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            var users = await _db.AppUsers
                .OrderBy(u => u.FullName)
                .Select(u => new AdminUserManagementViewModel
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int id, string role)
        {
            var allowedRoles = new[] { "Admin", "ProjectManager", "Developer" };

            if (string.IsNullOrWhiteSpace(role) || !allowedRoles.Contains(role))
            {
                TempData["Error"] = "Invalid role selected.";
                return RedirectToAction("UserManagement");
            }

            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(currentUserIdClaim) || !int.TryParse(currentUserIdClaim, out int currentUserId))
                return RedirectToAction("Login", "Account");

            var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("UserManagement");
            }

            // Prevent admin from removing own admin role accidentally
            if (user.Id == currentUserId && role != "Admin")
            {
                TempData["Error"] = "You cannot remove your own Admin role.";
                return RedirectToAction("UserManagement");
            }

            user.Role = role;
            await _db.SaveChangesAsync();

            TempData["Success"] = "User role updated successfully.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(currentUserIdClaim) || !int.TryParse(currentUserIdClaim, out int currentUserId))
                return RedirectToAction("Login", "Account");

            var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("UserManagement");
            }

            // Prevent admin from deleting own account
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("UserManagement");
            }

            _db.AppUsers.Remove(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "User deleted successfully.";
            return RedirectToAction("UserManagement");
        }

        [HttpGet]
        public async Task<IActionResult> LoginLogs()
        {
            var logs = await _db.LoginLogs
                .Include(x => x.AppUser)
                .OrderByDescending(x => x.LoginTime)
                .ToListAsync();

            return View(logs);
        }
    }
}