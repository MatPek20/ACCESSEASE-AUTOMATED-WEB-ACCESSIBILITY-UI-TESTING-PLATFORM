using AccessEase.Data;
using AccessEase.Models;
using AccessEase12.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessEase12.Controllers
{
    [Authorize(Roles = "ProjectManager")]
    public class UiValidationSettingsController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public UiValidationSettingsController(AccessEaseDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var projects = await _db.Projects
                .OrderBy(p => p.Name)
                .ToListAsync();

            var settings = await _db.ProjectUiValidationSettings
                .Include(x => x.Project)
                .ToListAsync();

            ViewBag.Settings = settings;
            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound();

            var setting = await _db.ProjectUiValidationSettings
                .FirstOrDefaultAsync(x => x.ProjectId == projectId);

            var vm = new ProjectUiValidationSettingViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                RequireHeader = setting?.RequireHeader ?? true,
                RequireMain = setting?.RequireMain ?? true,
                RequireFooter = setting?.RequireFooter ?? true,
                RequireNav = setting?.RequireNav ?? true,
                RequirePageTitle = setting?.RequirePageTitle ?? true,
                RequireSingleH1 = setting?.RequireSingleH1 ?? true,
                CheckUnlabeledInputs = setting?.CheckUnlabeledInputs ?? true,
                CheckDuplicateIds = setting?.CheckDuplicateIds ?? true,
                CheckHorizontalOverflow = setting?.CheckHorizontalOverflow ?? true,
                CheckTinyClickTargets = setting?.CheckTinyClickTargets ?? true,
                MinClickTargetSize = setting?.MinClickTargetSize ?? 32
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProjectUiValidationSettingViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId);
            if (project == null) return NotFound();

            var setting = await _db.ProjectUiValidationSettings
                .FirstOrDefaultAsync(x => x.ProjectId == model.ProjectId);

            if (setting == null)
            {
                setting = new ProjectUiValidationSetting
                {
                    ProjectId = model.ProjectId
                };
                _db.ProjectUiValidationSettings.Add(setting);
            }

            setting.RequireHeader = model.RequireHeader;
            setting.RequireMain = model.RequireMain;
            setting.RequireFooter = model.RequireFooter;
            setting.RequireNav = model.RequireNav;
            setting.RequirePageTitle = model.RequirePageTitle;
            setting.RequireSingleH1 = model.RequireSingleH1;
            setting.CheckUnlabeledInputs = model.CheckUnlabeledInputs;
            setting.CheckDuplicateIds = model.CheckDuplicateIds;
            setting.CheckHorizontalOverflow = model.CheckHorizontalOverflow;
            setting.CheckTinyClickTargets = model.CheckTinyClickTargets;
            setting.MinClickTargetSize = model.MinClickTargetSize;

            await _db.SaveChangesAsync();

            TempData["Success"] = "UI validation settings updated successfully.";
            return RedirectToAction("Index");
        }
    }
}