using AccessEase.Data;
using AccessEase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace AccessEase12.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AccessEaseDbContext _db;

        public HomeController(AccessEaseDbContext db)
        {
            _db = db;
        }

        private int GetCurrentUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out int userId))
                throw new UnauthorizedAccessException("Invalid or missing user identity.");

            return userId;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("Scan");
        }

        [AllowAnonymous]
        public IActionResult Welcome()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Developer"))
                    return RedirectToAction("Scan", "Home");

                if (User.IsInRole("ProjectManager") || User.IsInRole("Admin"))
                    return RedirectToAction("Dashboard", "Home");
            }

            return View();
        }

        [Authorize(Roles = "Developer")]
        [HttpGet]
        public IActionResult Scan()
        {
            var userId = GetCurrentUserId();

            var assignedProjects = _db.ProjectMembers
                .Include(pm => pm.Project)
                .Where(pm => pm.AppUserId == userId)
                .Select(pm => pm.Project!)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Projects = assignedProjects;
            ViewBag.AxeIssues = new List<AxeIssue>();
            ViewBag.UiIssues = new List<AxeIssue>();
            ViewBag.Mode = "axe";
            ViewBag.Severity = "all";
            return View();
        }

        [Authorize(Roles = "Developer")]
        [HttpPost]
        public IActionResult Scan(string url, string mode, string severity, int? projectId)
        {
            url = (url ?? "").Trim();
            mode = (mode ?? "axe").ToLower();
            severity = (severity ?? "all").ToLower();

            ViewBag.Mode = mode;
            ViewBag.Severity = severity;
            ViewBag.AxeIssues = new List<AxeIssue>();
            ViewBag.UiIssues = new List<AxeIssue>();

            var userId = GetCurrentUserId();

            var assignedProjects = _db.ProjectMembers
                .Include(pm => pm.Project)
                .Where(pm => pm.AppUserId == userId)
                .Select(pm => pm.Project!)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Projects = assignedProjects;

            if (!projectId.HasValue)
            {
                ViewBag.Error = "Please select a project before scanning.";
                return View();
            }

            bool isAssignedToProject = _db.ProjectMembers
                .Any(pm => pm.AppUserId == userId && pm.ProjectId == projectId.Value);

            if (!isAssignedToProject)
            {
                ViewBag.Error = "You are not assigned to the selected project.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                ViewBag.Error = "URL cannot be empty.";
                return View();
            }

            IWebDriver driver = null;

            try
            {
                EnsureStorageFolders();

                driver = CreateLocalChromeDriver();
                driver.Navigate().GoToUrl(url);
                Thread.Sleep(800);

                List<AxeIssue> axeResults = new();
                List<AxeIssue> uiResults = new();

                if (mode == "axe" || mode == "both")
                {
                    axeResults = RunAxeCoreScanWithEvidence(driver, severity, url);
                }

                if (mode == "ui" || mode == "both")
                {
                    var screenshotPath = SaveFullScreenshot(driver, url, "ui");

                    var uiSettings = _db.ProjectUiValidationSettings
                        .FirstOrDefault(x => x.ProjectId == projectId.Value);

                    uiResults = RunUiValidationBasic(driver, uiSettings);

                    foreach (var issue in uiResults)
                    {
                        issue.Rule = "ui-" + issue.Rule;
                        issue.ScreenshotUrl = screenshotPath;
                    }
                }

                var allIssues = axeResults.Concat(uiResults).ToList();

                var record = new ScanRecord
                {
                    Url = url,
                    ScanTime = DateTime.UtcNow,
                    IssueCount = allIssues.Count,
                    IssuesJson = JsonSerializer.Serialize(allIssues),
                    AppUserId = userId,
                    ProjectId = projectId.Value
                };

                _db.ScanRecords.Add(record);
                _db.SaveChanges();

                var remediationIssues = allIssues.Select(issue => new RemediationIssue
                {
                    ScanRecordId = record.Id,
                    Impact = issue.Impact ?? "",
                    Rule = issue.Rule ?? "",
                    Target = issue.Target,
                    FixTip = issue.FixTip,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _db.RemediationIssues.AddRange(remediationIssues);
                _db.SaveChanges();

                var baseline = _db.BaselineApprovals
                    .Include(b => b.ScanRecord)
                    .FirstOrDefault(b => b.ProjectId == projectId.Value && b.Url == url);

                if (baseline != null && baseline.ScanRecord != null)
                {
                    int baselineIssueCount = baseline.ScanRecord.IssueCount;
                    int currentIssueCount = record.IssueCount;
                    int difference = currentIssueCount - baselineIssueCount;

                    ViewBag.BaselineExists = true;
                    ViewBag.BaselineScanId = baseline.ScanRecordId;
                    ViewBag.BaselineIssueCount = baselineIssueCount;
                    ViewBag.CurrentIssueCount = currentIssueCount;
                    ViewBag.IssueDifference = difference;

                    if (difference < 0)
                    {
                        ViewBag.BaselineComparisonStatus = "Improved";
                        ViewBag.BaselineComparisonMessage = $"Improved: current scan found {-difference} fewer issue(s) than the approved baseline.";
                    }
                    else if (difference > 0)
                    {
                        ViewBag.BaselineComparisonStatus = "Regressed";
                        ViewBag.BaselineComparisonMessage = $"Regressed: current scan found {difference} more issue(s) than the approved baseline.";
                    }
                    else
                    {
                        ViewBag.BaselineComparisonStatus = "NoChange";
                        ViewBag.BaselineComparisonMessage = "No Change: current scan has the same number of issues as the approved baseline.";
                    }
                }
                else
                {
                    ViewBag.BaselineExists = false;
                }

                ViewBag.AxeIssues = axeResults;
                ViewBag.UiIssues = uiResults;
                ViewBag.Success = "Scan completed successfully.";

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.InnerException?.Message ?? ex.Message;
                return View();
            }
            finally
            {
                try { driver?.Quit(); } catch { }
            }
        }

        [Authorize(Roles = "Developer")]
        [HttpGet]
        public IActionResult History()
        {
            var userId = GetCurrentUserId();

            var history = _db.ScanRecords
                .Where(x => x.AppUserId == userId)
                .OrderByDescending(x => x.ScanTime)
                .ToList();

            return View(history);
        }

        [Authorize(Roles = "Developer")]
        [HttpGet]
        public IActionResult HistoryDetails(int id)
        {
            var currentUserId = GetCurrentUserId();
            bool isProjectManager = User.IsInRole("ProjectManager");

            var scanQuery = _db.ScanRecords
                .Include(x => x.AppUser)
                .AsQueryable();

            if (!isProjectManager)
            {
                scanQuery = scanQuery.Where(x => x.AppUserId == currentUserId);
            }

            var scan = scanQuery.FirstOrDefault(x => x.Id == id);
            if (scan == null) return NotFound();

            var issues = JsonSerializer.Deserialize<List<AxeIssue>>(scan.IssuesJson) ?? new List<AxeIssue>();

            var historyQuery = _db.ScanRecords
                .Where(s => s.Url == scan.Url);

            if (!isProjectManager)
            {
                historyQuery = historyQuery.Where(s => s.AppUserId == currentUserId);
            }
            else
            {
                historyQuery = historyQuery.Where(s => s.AppUserId == scan.AppUserId);
            }

            var history = historyQuery
                .OrderBy(s => s.ScanTime)
                .ToList();

            ViewBag.Issues = issues;
            ViewBag.TrendLabels = history.Select(h => h.ScanTime.ToString("MM/dd")).ToList();
            ViewBag.TrendValues = history.Select(h => h.IssueCount).ToList();
            ViewBag.SeverityData = new List<int>
            {
                issues.Count(i => i.Impact == "critical"),
                issues.Count(i => i.Impact == "serious"),
                issues.Count(i => i.Impact == "moderate"),
                issues.Count(i => i.Impact == "minor")
            };

            ViewBag.Url = scan.Url;
            ViewBag.Time = scan.ScanTime.ToString("g");
            ViewBag.IssueCount = scan.IssueCount;
            ViewBag.Id = scan.Id;
            ViewBag.ScannedBy = scan.AppUser?.FullName ?? "Unknown";

            return View(scan);
        }

        [HttpGet]
        public IActionResult ExportCsv(int id)
        {
            var currentUserId = GetCurrentUserId();
            bool isProjectManager = User.IsInRole("ProjectManager");

            var query = _db.ScanRecords.AsQueryable();

            if (!isProjectManager)
            {
                query = query.Where(s => s.AppUserId == currentUserId);
            }

            var record = query.FirstOrDefault(s => s.Id == id);
            if (record == null) return NotFound();

            var issues = JsonSerializer.Deserialize<List<AxeIssue>>(record.IssuesJson) ?? new List<AxeIssue>();

            var builder = new StringBuilder();
            builder.AppendLine("Impact,Rule,Count,Target,Fix Suggestion,Help URL");

            foreach (var issue in issues)
            {
                string cleanTarget = (issue.Target ?? "").Replace(",", ";").Replace("\r\n", " ");
                string cleanFix = (issue.FixTip ?? "").Replace(",", ";").Replace("\r\n", " ");

                builder.AppendLine($"{issue.Impact},{issue.Rule},{issue.Count},{cleanTarget},{cleanFix},{issue.HelpUrl}");
            }

            var fileName = $"ScanReport_{record.Id}_{DateTime.Now:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        private IWebDriver CreateLocalChromeDriver()
        {
            var options = new ChromeOptions();

            if (OperatingSystem.IsLinux())
            {
                options.BinaryLocation = "/usr/bin/google-chrome";
            }

            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--ignore-certificate-errors");

            return new ChromeDriver(options);
        }

        private void EnsureStorageFolders()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage"));
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage", "ui"));
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Storage", "evidence"));
        }

        private string UrlKey(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url.Trim().ToLower()));
            return Convert.ToHexString(bytes).ToLower();
        }

        private byte[] CaptureScreenshotBytes(IWebDriver driver)
        {
            return ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
        }

        private string SaveFullScreenshot(IWebDriver driver, string url, string folder)
        {
            var key = UrlKey(url);
            var fileName = $"{key}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var physical = Path.Combine(Directory.GetCurrentDirectory(), "Storage", folder, fileName);
            System.IO.File.WriteAllBytes(physical, CaptureScreenshotBytes(driver));

            return $"/storage/{folder}/{fileName}";
        }

        private string TryGetOuterHtmlSnippet(IWebDriver driver, string cssSelector)
        {
            try
            {
                var el = driver.FindElement(By.CssSelector(cssSelector));
                var html = el.GetAttribute("outerHTML") ?? "";
                html = html.Replace("\r", " ").Replace("\n", " ").Trim();
                if (html.Length > 180) html = html.Substring(0, 180) + "...";
                return html;
            }
            catch
            {
                return "";
            }
        }

        private string TryCaptureElementEvidence(IWebDriver driver, string cssSelector, string url)
        {
            try
            {
                var el = driver.FindElement(By.CssSelector(cssSelector));

                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "arguments[0].scrollIntoView({block:'center', inline:'nearest'});", el);
                Thread.Sleep(300);

                var bytes = CaptureScreenshotBytes(driver);

                using var img = Image.Load<Rgba32>(bytes);

                var loc = el.Location;
                var size = el.Size;

                int x = Math.Max(loc.X, 0);
                int y = Math.Max(loc.Y, 0);
                int w = Math.Min(size.Width, img.Width - x);
                int h = Math.Min(size.Height, img.Height - y);

                if (w <= 0 || h <= 0) return "";

                int pad = 6;
                x = Math.Max(x - pad, 0);
                y = Math.Max(y - pad, 0);
                w = Math.Min(w + pad * 2, img.Width - x);
                h = Math.Min(h + pad * 2, img.Height - y);

                img.Mutate(c => c.Crop(new Rectangle(x, y, w, h)));

                var key = UrlKey(url);
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 6);
                var fileName = $"{key}_{DateTime.Now:yyyyMMdd_HHmmss}_{shortId}.png";

                var physical = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "evidence", fileName);
                img.Save(physical);

                return $"/storage/evidence/{fileName}";
            }
            catch
            {
                return "";
            }
        }

        private int? TryFindApproxHtmlLine(IWebDriver driver, string targetSelector, string htmlSnippet)
        {
            try
            {
                var pageSource = driver.PageSource;
                if (string.IsNullOrWhiteSpace(pageSource))
                    return null;

                string searchText = "";

                if (!string.IsNullOrWhiteSpace(htmlSnippet))
                {
                    searchText = htmlSnippet.Length > 120 ? htmlSnippet.Substring(0, 120) : htmlSnippet;
                }

                if (string.IsNullOrWhiteSpace(searchText) && !string.IsNullOrWhiteSpace(targetSelector))
                {
                    try
                    {
                        var el = driver.FindElement(By.CssSelector(targetSelector));
                        var outer = el.GetAttribute("outerHTML") ?? "";
                        searchText = outer.Length > 120 ? outer.Substring(0, 120) : outer;
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(searchText))
                    return null;

                var index = pageSource.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return null;

                int line = 1;
                for (int i = 0; i < index; i++)
                {
                    if (pageSource[i] == '\n')
                        line++;
                }

                return line;
            }
            catch
            {
                return null;
            }
        }

        private List<AxeIssue> RunUiValidationBasic(IWebDriver driver, ProjectUiValidationSetting? settings = null)
        {
            settings ??= new ProjectUiValidationSetting();

            var issues = new List<AxeIssue>();
            var js = (IJavaScriptExecutor)driver;

            bool hasHeader = driver.FindElements(By.TagName("header")).Any();
            bool hasMain = driver.FindElements(By.TagName("main")).Any() ||
                           driver.FindElements(By.CssSelector("[role='main']")).Any();
            bool hasFooter = driver.FindElements(By.TagName("footer")).Any();
            bool hasNav = driver.FindElements(By.TagName("nav")).Any() ||
                          driver.FindElements(By.CssSelector("[role='navigation']")).Any();

            if (settings.RequireHeader && !hasHeader)
                issues.Add(NewUiIssue("moderate", "landmark-header-missing",
                    "Add a <header> (or role=banner) for consistent page structure."));

            if (settings.RequireMain && !hasMain)
                issues.Add(NewUiIssue("serious", "landmark-main-missing",
                    "Add <main> (or role=main). This improves structure and navigation."));

            if (settings.RequireFooter && !hasFooter)
                issues.Add(NewUiIssue("minor", "landmark-footer-missing",
                    "Add a <footer> to complete the page structure."));

            if (settings.RequireNav && !hasNav)
                issues.Add(NewUiIssue("moderate", "landmark-nav-missing",
                    "Add <nav> (or role=navigation) for primary navigation links."));

            if (settings.RequirePageTitle)
            {
                try
                {
                    var title = driver.Title?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        issues.Add(NewUiIssue("serious", "page-title-missing",
                            "Add a meaningful page title so users can identify the page clearly."));
                    }
                }
                catch { }
            }

            if (settings.RequireSingleH1)
            {
                var h1Count = driver.FindElements(By.TagName("h1")).Count;
                if (h1Count == 0)
                {
                    issues.Add(NewUiIssue("moderate", "h1-missing",
                        "Add one main H1 heading to clearly identify the page content."));
                }
                else if (h1Count > 1)
                {
                    issues.Add(NewUiIssue("moderate", "multiple-h1",
                        "Use only one main H1 heading per page for clearer hierarchy."));
                }
            }

            if (settings.CheckUnlabeledInputs)
            {
                var inputs = driver.FindElements(By.CssSelector("input, select, textarea")).ToList();
                int unlabeled = 0;

                foreach (var el in inputs)
                {
                    try
                    {
                        var id = el.GetAttribute("id");
                        var ariaLabel = el.GetAttribute("aria-label");
                        var ariaLabelledBy = el.GetAttribute("aria-labelledby");

                        bool hasLabel = false;

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            hasLabel = driver.FindElements(By.CssSelector($"label[for='{CssEscape(id)}']")).Any();
                        }

                        if (!hasLabel && string.IsNullOrWhiteSpace(ariaLabel) && string.IsNullOrWhiteSpace(ariaLabelledBy))
                            unlabeled++;
                    }
                    catch { }
                }

                if (unlabeled > 0)
                {
                    issues.Add(NewUiIssue("serious", "form-unlabeled-input",
                        $"{unlabeled} form field(s) do not have a label or aria-label."));
                }
            }

            if (settings.CheckDuplicateIds)
            {
                try
                {
                    int duplicateIds = Convert.ToInt32(js.ExecuteScript(@"
                const ids = [...document.querySelectorAll('[id]')].map(x => x.id);
                const dupes = ids.filter((id, index) => ids.indexOf(id) !== index);
                return [...new Set(dupes)].length;
            "));
                    if (duplicateIds > 0)
                    {
                        issues.Add(NewUiIssue("serious", "duplicate-id",
                            $"Found {duplicateIds} duplicate id value(s). IDs must be unique."));
                    }
                }
                catch { }
            }

            if (settings.CheckHorizontalOverflow)
            {
                try
                {
                    bool overflowX = (bool)js.ExecuteScript(@"
                return document.documentElement.scrollWidth > document.documentElement.clientWidth + 5;
            ");
                    if (overflowX)
                    {
                        issues.Add(NewUiIssue("moderate", "layout-horizontal-overflow",
                            "Page has horizontal overflow. Check container widths, images, and fixed elements."));
                    }
                }
                catch { }
            }

            if (settings.CheckTinyClickTargets)
            {
                try
                {
                    int minSize = settings.MinClickTargetSize;

                    int tinyTargets = Convert.ToInt32(js.ExecuteScript(@"
                const minSize = arguments[0];
                const els = Array.from(document.querySelectorAll('button, a, input[type=button], input[type=submit]'));
                return els.filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0 && (r.width < minSize || r.height < minSize);
                }).length;
            ", minSize));

                    if (tinyTargets > 0)
                    {
                        issues.Add(NewUiIssue("moderate", "tiny-click-target",
                            $"Found {tinyTargets} clickable element(s) smaller than {minSize}px."));
                    }
                }
                catch { }
            }

            // 1. Broken image detection
            try
            {
                int brokenImages = Convert.ToInt32(js.ExecuteScript(@"
            const imgs = Array.from(document.images || []);
            return imgs.filter(img => img.src && (!img.complete || img.naturalWidth === 0)).length;
        "));

                if (brokenImages > 0)
                {
                    issues.Add(NewUiIssue("serious", "broken-image",
                        $"Found {brokenImages} broken image(s) that failed to load properly."));
                }
            }
            catch { }

            // 2. Empty button detection
            try
            {
                int emptyButtons = Convert.ToInt32(js.ExecuteScript(@"
            const buttons = Array.from(document.querySelectorAll('button, input[type=button], input[type=submit], input[type=reset]'));
            return buttons.filter(el => {
                const text = (el.innerText || el.value || '').trim();
                const aria = (el.getAttribute('aria-label') || '').trim();
                const title = (el.getAttribute('title') || '').trim();
                return !text && !aria && !title;
            }).length;
        "));

                if (emptyButtons > 0)
                {
                    issues.Add(NewUiIssue("serious", "empty-button",
                        $"Found {emptyButtons} button(s) without visible text or accessible label."));
                }
            }
            catch { }

            // 2. Empty link detection
            try
            {
                int emptyLinks = Convert.ToInt32(js.ExecuteScript(@"
            const links = Array.from(document.querySelectorAll('a[href]'));
            return links.filter(el => {
                const text = (el.innerText || '').trim();
                const aria = (el.getAttribute('aria-label') || '').trim();
                const title = (el.getAttribute('title') || '').trim();
                return !text && !aria && !title;
            }).length;
        "));

                if (emptyLinks > 0)
                {
                    issues.Add(NewUiIssue("serious", "empty-link",
                        $"Found {emptyLinks} link(s) without visible text or accessible label."));
                }
            }
            catch { }

            // 3. Button size consistency
            try
            {
                int inconsistentButtons = Convert.ToInt32(js.ExecuteScript(@"
            const buttons = Array.from(document.querySelectorAll('button, a.btn, input[type=button], input[type=submit]'))
                .filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0;
                });

            if (buttons.length < 2) return 0;

            const heights = buttons.map(el => Math.round(el.getBoundingClientRect().height));
            const minH = Math.min(...heights);
            const maxH = Math.max(...heights);

            return (maxH - minH) > 12 ? 1 : 0;
        "));

                if (inconsistentButtons > 0)
                {
                    issues.Add(NewUiIssue("moderate", "button-inconsistent-size",
                        "Buttons appear to have inconsistent heights. Use a more consistent button size/style across the page."));
                }
            }
            catch { }

            // 4. Input size consistency
            try
            {
                int inconsistentInputs = Convert.ToInt32(js.ExecuteScript(@"
            const inputs = Array.from(document.querySelectorAll('input[type=text], input[type=email], input[type=password], input[type=url], input[type=search], input[type=tel], input[type=number], select, textarea'))
                .filter(el => {
                    const r = el.getBoundingClientRect();
                    return r.width > 0 && r.height > 0;
                });

            if (inputs.length < 2) return 0;

            const heights = inputs.map(el => Math.round(el.getBoundingClientRect().height));
            const minH = Math.min(...heights);
            const maxH = Math.max(...heights);

            return (maxH - minH) > 12 ? 1 : 0;
        "));

                if (inconsistentInputs > 0)
                {
                    issues.Add(NewUiIssue("moderate", "input-inconsistent-size",
                        "Form inputs appear to have inconsistent heights. Use a more consistent input size/style across the page."));
                }
            }
            catch { }

            if (!issues.Any())
            {
                issues.Add(NewUiIssue("minor", "ui-ok",
                    "No UI validation issues detected by the current configured rule set."));
            }

            return issues;
        }

        private string CssEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private AxeIssue NewUiIssue(string impact, string rule, string fixTip)
        {
            return new AxeIssue
            {
                Impact = impact,
                Rule = rule,
                Count = 1,
                Target = "-",
                HelpUrl = "",
                Help = "UI validation rule",
                FixTip = fixTip,
                HtmlSnippet = "",
                EvidenceUrl = "",
                ScreenshotUrl = "",
                ApproxHtmlLine = null,
                Category = GetUiCategory(rule)
            };
        }

        private List<AxeIssue> RunAxeCoreScanWithEvidence(IWebDriver driver, string severity, string url)
        {
            var axePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "js", "axe.min.js");
            if (!System.IO.File.Exists(axePath))
                throw new Exception("axe.min.js not found. Put it in wwwroot/js/axe.min.js");

            var axeJs = System.IO.File.ReadAllText(axePath);
            var js = (IJavaScriptExecutor)driver;

            js.ExecuteScript(axeJs);

            var json = (string)js.ExecuteAsyncScript(@"
                const done = arguments[arguments.length - 1];
                axe.run().then(res => done(JSON.stringify(res)))
                        .catch(err => done(JSON.stringify({ error: err.toString() })));
            ");

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out var errEl))
                throw new Exception("axe.run() error: " + (errEl.GetString() ?? "unknown"));

            var results = new List<AxeIssue>();
            var violations = doc.RootElement.GetProperty("violations");

            foreach (var v in violations.EnumerateArray())
            {
                var ruleId = v.GetProperty("id").GetString() ?? "unknown";
                var impact = (v.TryGetProperty("impact", out var impEl) ? (impEl.GetString() ?? "unknown") : "unknown")
                    .ToLower();

                if (!PassSeverityFilter(impact, severity))
                    continue;

                var helpUrl = v.TryGetProperty("helpUrl", out var helpEl) ? (helpEl.GetString() ?? "") : "";
                var helpText = v.TryGetProperty("help", out var helpTextEl) ? (helpTextEl.GetString() ?? "") : "";

                var nodes = v.GetProperty("nodes");
                int count = nodes.GetArrayLength();

                string target = "";
                try
                {
                    var firstNode = nodes[0];
                    if (firstNode.TryGetProperty("target", out var targets)
                        && targets.ValueKind == JsonValueKind.Array
                        && targets.GetArrayLength() > 0)
                    {
                        target = targets[0].GetString() ?? "";
                    }
                }
                catch { }

                string evidenceUrl = "";
                string htmlSnippet = "";
                int? approxHtmlLine = null;

                if (!string.IsNullOrWhiteSpace(target))
                {
                    evidenceUrl = TryCaptureElementEvidence(driver, target, url);
                    htmlSnippet = TryGetOuterHtmlSnippet(driver, target);
                    approxHtmlLine = TryFindApproxHtmlLine(driver, target, htmlSnippet);
                }

                results.Add(new AxeIssue
                {
                    Impact = impact,
                    Rule = ruleId,
                    Count = count,
                    HelpUrl = helpUrl,
                    Help = helpText,
                    Target = string.IsNullOrWhiteSpace(target) ? "-" : target,
                    FixTip = GetFixTip(ruleId),
                    EvidenceUrl = evidenceUrl,
                    HtmlSnippet = htmlSnippet,
                    ScreenshotUrl = "",
                    ApproxHtmlLine = approxHtmlLine
                });
            }

            return results;
        }

        private bool PassSeverityFilter(string impact, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "all") return true;
            return impact == filter;
        }

        private string GetFixTip(string ruleId)
        {
            ruleId = (ruleId ?? "").Trim().ToLower();

            return ruleId switch
            {
                "color-contrast" => "Increase contrast between text and background (normal + hover).",
                "image-alt" => "Add meaningful alt text. If decorative, use alt=\"\".",
                "label" => "Add <label for> or aria-label / aria-labelledby for inputs.",
                "link-name" => "Make links have readable text (or aria-label). Avoid empty/icon-only links.",
                "button-name" => "Buttons need an accessible name: visible text or aria-label.",
                "heading-order" => "Use headings in logical order (don’t skip H2 → H4 etc).",
                "landmark-one-main" => "Ensure exactly one main landmark exists (<main> or role=\"main\").",
                "region" => "Wrap sections with landmarks or add aria-labelledby.",
                _ => "Open the WCAG link and apply the recommended fix for this rule."
            };
        }

        [Authorize(Roles = "Developer")]
        [HttpPost]
        public IActionResult ClearHistory()
        {
            var userId = GetCurrentUserId();

            var all = _db.ScanRecords.Where(x => x.AppUserId == userId).ToList();
            _db.ScanRecords.RemoveRange(all);
            _db.SaveChanges();

            TempData["Success"] = "Scan history cleared.";
            return RedirectToAction("History");
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            if (User.IsInRole("Admin"))
            {
                var allUsers = _db.AppUsers.ToList();
                var allScans = _db.ScanRecords
                    .OrderBy(s => s.ScanTime)
                    .ToList();

                ViewBag.DashboardType = "Admin";

                ViewBag.TotalUsers = allUsers.Count;
                ViewBag.TotalAdmins = allUsers.Count(u => u.Role == "Admin");
                ViewBag.TotalProjectManagers = allUsers.Count(u => u.Role == "ProjectManager");
                ViewBag.TotalDevelopers = allUsers.Count(u => u.Role == "Developer");
                ViewBag.TotalScans = allScans.Count;

                var recentScans = allScans.TakeLast(15).ToList();
                ViewBag.TrendLabels = recentScans.Select(s => s.ScanTime.ToString("MM/dd HH:mm")).ToList();
                ViewBag.TrendValues = recentScans.Select((s, i) => i + 1).ToList();

                return View();
            }

            if (User.IsInRole("ProjectManager"))
            {
                var allScans = _db.ScanRecords
                    .Include(s => s.Project)
                    .Include(s => s.AppUser)
                    .OrderBy(s => s.ScanTime)
                    .ToList();

                ViewBag.DashboardType = "ProjectManager";

                ViewBag.TotalScans = allScans.Count;
                ViewBag.TotalProjects = _db.Projects.Count();
                ViewBag.PendingIssues = _db.RemediationIssues.Count(r => r.Status == "Pending");
                ViewBag.ResolvedIssues = _db.RemediationIssues.Count(r => r.Status == "Fixed");

                var recentScans = allScans.TakeLast(15).ToList();
                ViewBag.TrendLabels = recentScans.Select(s => s.ScanTime.ToString("MM/dd HH:mm")).ToList();
                ViewBag.TrendValues = recentScans.Select(s => s.IssueCount).ToList();

                return View();
            }

            int currentUserId = GetCurrentUserId();

            var myScans = _db.ScanRecords
                .Where(s => s.AppUserId == currentUserId)
                .OrderBy(s => s.ScanTime)
                .ToList();

            ViewBag.DashboardType = "Developer";

            ViewBag.TotalScans = myScans.Count;
            ViewBag.AvgIssues = myScans.Any() ? myScans.Average(s => s.IssueCount) : 0;
            ViewBag.MyProjects = _db.ProjectMembers.Count(pm => pm.AppUserId == currentUserId);

            var myRecentScans = myScans.TakeLast(15).ToList();
            ViewBag.TrendLabels = myRecentScans.Select(s => s.ScanTime.ToString("MM/dd HH:mm")).ToList();
            ViewBag.TrendValues = myRecentScans.Select(s => s.IssueCount).ToList();

            return View();
        }

        [Authorize(Roles = "Developer,ProjectManager")]
        [HttpGet]
        public IActionResult Reports(string? urlFilter, int? userFilter, int? projectFilter)
        {
            var currentUserId = GetCurrentUserId();
            bool isProjectManager = User.IsInRole("ProjectManager");

            var query = _db.ScanRecords
                .Include(s => s.AppUser)
                .Include(s => s.Project)
                .AsQueryable();

            if (!isProjectManager)
            {
                query = query.Where(s => s.AppUserId == currentUserId);
            }

            if (isProjectManager && userFilter.HasValue)
            {
                query = query.Where(s => s.AppUserId == userFilter.Value);
            }

            if (projectFilter.HasValue)
            {
                query = query.Where(s => s.ProjectId == projectFilter.Value);
            }

            var allRecords = query
                .OrderBy(s => s.ScanTime)
                .ToList();

            ViewBag.Users = isProjectManager
                ? _db.AppUsers.OrderBy(u => u.FullName).ToList()
                : new List<AppUser>();

            ViewBag.Projects = isProjectManager
                ? _db.Projects.OrderBy(p => p.Name).ToList()
                : _db.ProjectMembers
                    .Include(pm => pm.Project)
                    .Where(pm => pm.AppUserId == currentUserId)
                    .Select(pm => pm.Project!)
                    .OrderBy(p => p.Name)
                    .ToList();

            ViewBag.CurrentUserFilter = userFilter;
            ViewBag.CurrentProjectFilter = projectFilter;
            ViewBag.CurrentFilter = urlFilter;

            var workingList = allRecords;

            if (!string.IsNullOrWhiteSpace(urlFilter))
            {
                workingList = workingList.Where(r => r.Url == urlFilter).ToList();
            }

            ViewBag.UniqueUrls = workingList.Select(r => r.Url).Distinct().ToList();

            if (!workingList.Any())
            {
                ViewBag.TotalScans = 0;
                ViewBag.AvgIssues = 0.0;
                ViewBag.TrendLabels = new List<string>();
                ViewBag.TrendValues = new List<int>();
                ViewBag.BaselineExists = false;
                return View(new List<ScanRecord>());
            }

            // ADD THIS PART HERE
            var latestRecord = workingList
                .OrderByDescending(x => x.ScanTime)
                .FirstOrDefault();

            if (latestRecord != null)
            {
                var baseline = _db.BaselineApprovals
                    .Include(b => b.ScanRecord)
                    .FirstOrDefault(b =>
                        b.ProjectId == latestRecord.ProjectId &&
                        b.Url == latestRecord.Url);

                if (baseline != null && baseline.ScanRecord != null)
                {
                    int baselineIssueCount = baseline.ScanRecord.IssueCount;
                    int currentIssueCount = latestRecord.IssueCount;
                    int difference = currentIssueCount - baselineIssueCount;

                    string status;
                    string message;

                    if (difference < 0)
                    {
                        status = "Improved";
                        message = $"Improved: latest report shows {-difference} fewer issue(s) than the approved baseline.";
                    }
                    else if (difference > 0)
                    {
                        status = "Regressed";
                        message = $"Regressed: latest report shows {difference} more issue(s) than the approved baseline.";
                    }
                    else
                    {
                        status = "NoChange";
                        message = "No Change: latest report has the same number of issues as the approved baseline.";
                    }

                    ViewBag.BaselineExists = true;
                    ViewBag.BaselineScanId = baseline.ScanRecordId;
                    ViewBag.BaselineIssueCount = baselineIssueCount;
                    ViewBag.CurrentIssueCount = currentIssueCount;
                    ViewBag.IssueDifference = difference;
                    ViewBag.BaselineComparisonStatus = status;
                    ViewBag.BaselineComparisonMessage = message;
                    ViewBag.BaselineUrl = latestRecord.Url;
                    ViewBag.BaselineProjectName = latestRecord.Project?.Name ?? "-";
                }
                else
                {
                    ViewBag.BaselineExists = false;
                }
            }
            else
            {
                ViewBag.BaselineExists = false;
            }

            var lastScans = workingList.TakeLast(15).ToList();
            ViewBag.TrendLabels = lastScans.Select(s => s.ScanTime.ToString("MM/dd HH:mm")).ToList();
            ViewBag.TrendValues = lastScans.Select(s => s.IssueCount).ToList();

            ViewBag.TotalScans = workingList.Count;
            ViewBag.AvgIssues = workingList.Average(x => x.IssueCount);

            return View(workingList);
        }

        private string GetUiCategory(string rule)
        {
            rule = (rule ?? "").Trim().ToLower();

            return rule switch
            {
                "landmark-header-missing" => "Layout",
                "landmark-main-missing" => "Layout",
                "landmark-footer-missing" => "Layout",
                "landmark-nav-missing" => "Layout",
                "h1-missing" => "Content",
                "multiple-h1" => "Content",
                "page-title-missing" => "Content",
                "form-unlabeled-input" => "Forms",
                "duplicate-id" => "Forms",
                "broken-image" => "Content",
                "empty-button" => "Interaction",
                "empty-link" => "Interaction",
                "button-inconsistent-size" => "Visual Consistency",
                "input-inconsistent-size" => "Visual Consistency",
                "layout-horizontal-overflow" => "Layout",
                "tiny-click-target" => "Interaction",
                "ui-ok" => "General",
                _ => "General"
            };
        }

        [Authorize(Roles = "Developer,ProjectManager")]
        [HttpGet]
        public IActionResult CompareScans(int? scanAId, int? scanBId)
        {
            var currentUserId = GetCurrentUserId();
            bool isProjectManager = User.IsInRole("ProjectManager");

            var scanQuery = _db.ScanRecords
                .Include(s => s.AppUser)
                .Include(s => s.Project)
                .OrderByDescending(s => s.ScanTime)
                .AsQueryable();

            if (!isProjectManager)
            {
                scanQuery = scanQuery.Where(s => s.AppUserId == currentUserId);
            }

            var availableScans = scanQuery.ToList();

            var vm = new AccessEase12.ViewModels.CompareScansViewModel
            {
                AvailableScans = availableScans,
                ScanAId = scanAId,
                ScanBId = scanBId
            };

            if (!scanAId.HasValue || !scanBId.HasValue)
                return View(vm);

            var scanA = availableScans.FirstOrDefault(s => s.Id == scanAId.Value);
            var scanB = availableScans.FirstOrDefault(s => s.Id == scanBId.Value);

            if (scanA == null || scanB == null)
            {
                ViewBag.Error = "One or both selected scans could not be found.";
                return View(vm);
            }

            var issuesA = DeserializeIssues(scanA.IssuesJson);
            var issuesB = DeserializeIssues(scanB.IssuesJson);

            var mapA = issuesA.ToDictionary(i => GetIssueCompareKey(i), i => i);
            var mapB = issuesB.ToDictionary(i => GetIssueCompareKey(i), i => i);

            var newIssues = mapB
                .Where(x => !mapA.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToList();

            var fixedIssues = mapA
                .Where(x => !mapB.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToList();

            var unchangedIssues = mapB
                .Where(x => mapA.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToList();

            string resultStatus;
            if (newIssues.Count == 0 && fixedIssues.Count > 0)
                resultStatus = "Improved";
            else if (newIssues.Count > 0 && fixedIssues.Count == 0)
                resultStatus = "Regressed";
            else if (newIssues.Count == 0 && fixedIssues.Count == 0)
                resultStatus = "No Change";
            else
                resultStatus = "Mixed";

            vm.ScanA = scanA;
            vm.ScanB = scanB;
            vm.ScanAIssueCount = issuesA.Count;
            vm.ScanBIssueCount = issuesB.Count;
            vm.NewIssuesCount = newIssues.Count;
            vm.FixedIssuesCount = fixedIssues.Count;
            vm.UnchangedIssuesCount = unchangedIssues.Count;
            vm.ResultStatus = resultStatus;
            vm.NewIssues = newIssues;
            vm.FixedIssues = fixedIssues;
            vm.UnchangedIssues = unchangedIssues;

            return View(vm);
        }

        private List<AxeIssue> DeserializeIssues(string issuesJson)
        {
            if (string.IsNullOrWhiteSpace(issuesJson))
                return new List<AxeIssue>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<AxeIssue>>(issuesJson)
                       ?? new List<AxeIssue>();
            }
            catch
            {
                return new List<AxeIssue>();
            }
        }

        private string GetIssueCompareKey(AxeIssue issue)
        {
            var rule = issue.Rule?.Trim().ToLower() ?? "";
            var target = issue.Target?.Trim().ToLower() ?? "";
            return $"{rule}|{target}";
        }
    }
}