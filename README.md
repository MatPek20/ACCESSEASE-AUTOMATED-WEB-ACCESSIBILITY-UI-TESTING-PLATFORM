# AccessEase

### Automated Web Accessibility & UI Testing Platform

AccessEase is a web-based automated testing platform designed to combine **web accessibility validation** and **visual UI testing** into a single workflow.

The system helps developers and project teams identify accessibility issues based on **WCAG 2.1**, while also detecting visual inconsistencies such as layout shifts, rendering problems, and UI regressions.

Instead of relying on separate tools for accessibility and visual testing, AccessEase brings both testing approaches together in one platform.

---

## Features

### Accessibility Testing

* Automated WCAG 2.1 accessibility scanning
* Detection of common accessibility issues
* Missing alternative text detection
* Incorrect ARIA attribute detection
* Colour contrast validation
* Keyboard navigation checks
* Severity-based issue reporting
* Detailed issue descriptions and suggested improvements

### Visual UI Testing

* Automated browser-based UI testing
* Website screenshot capture
* Baseline screenshot comparison
* Detection of layout shifts and visual inconsistencies
* Visual regression detection
* Comparison between previous and current scan results

### Project Management

* Create and manage testing projects
* Assign website URLs to projects
* Organise scan records by project
* Monitor testing progress
* Track issue remediation status

### Scan & Results

* Run accessibility scans
* Run visual UI validation
* Run both testing methods together
* View detailed scan results
* Maintain scan history
* Compare previous scan results
* Identify new, fixed, and unchanged issues

### Reporting

* Generate testing reports
* View scan statistics
* Track issue trends
* Export testing results for documentation and review

### Baseline & Remediation

* Approve a scan as a visual testing baseline
* Compare future scans against an approved baseline
* Track detected issues through remediation stages
* Supported remediation statuses:

  * Pending
  * In Progress
  * On Hold
  * Fixed

### Role-Based Access

AccessEase supports multiple user roles:

| Role                     | Description                                                           |
| ------------------------ | --------------------------------------------------------------------- |
| **Developer**            | Performs accessibility and visual UI testing                          |
| **Project Manager**      | Manages projects, reviews results, reports, baselines and remediation |
| **System Administrator** | Manages users, roles and access permissions                           |
| **CI/CD System**         | Triggers automated testing through development/deployment pipelines   |

---

## Technology Stack

| Component             | Technology                           |
| --------------------- | ------------------------------------ |
| Backend               | ASP.NET Core / C#                    |
| Frontend              | Razor Views, Bootstrap 5, JavaScript |
| Browser Automation    | Selenium WebDriver                   |
| Accessibility Testing | axe-core / WCAG 2.1-based validation |
| Visual Testing        | OpenCV                               |
| ORM                   | Entity Framework Core                |
| Database              | PostgreSQL                           |
| Charts                | Chart.js                             |
| Browser Drivers       | ChromeDriver, Firefox GeckoDriver    |
| Version Control       | Git / GitHub                         |

The project uses ASP.NET Core MVC, Razor Views, Entity Framework Core, Selenium WebDriver and axe-core as core implementation technologies, with PostgreSQL used for data management.

---

## System Workflow

```text
                    ┌─────────────────────┐
                    │     User Login      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │      Dashboard      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ Select / Manage     │
                    │      Project        │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │     Submit URL      │
                    └──────────┬──────────┘
                               │
                 ┌─────────────┴─────────────┐
                 ▼                           ▼
       ┌──────────────────┐        ┌──────────────────┐
       │ Accessibility    │        │   Visual UI      │
       │     Scan         │        │   Validation     │
       └────────┬─────────┘        └────────┬─────────┘
                │                           │
                └─────────────┬─────────────┘
                              ▼
                   ┌─────────────────────┐
                   │    Scan Results     │
                   └──────────┬──────────┘
                              │
                ┌─────────────┼─────────────┐
                ▼             ▼             ▼
          ┌──────────┐  ┌──────────┐  ┌──────────────┐
          │ Reports  │  │ Compare  │  │ Remediation  │
          │          │  │  Scans   │  │   Tracking   │
          └──────────┘  └──────────┘  └──────────────┘
```

---

## Main Modules

* Authentication
* Dashboard
* User Management
* Project Management
* Accessibility Scanning
* Visual UI Validation
* Scan History & Results
* Reports
* Baseline Approval
* Scan Comparison
* Remediation Tracking
* Profile & Account Management

The system design includes a web frontend, backend, PostgreSQL database, accessibility testing component, visual UI testing component, target website, and CI/CD integration.

---

## Accessibility Testing

AccessEase focuses on common WCAG 2.1-related issues, including:

* Missing alternative text
* Incorrect ARIA attributes
* Poor colour contrast
* Keyboard navigation problems
* Other detected accessibility violations

The accessibility results provide information such as the issue type, severity, affected element, description, and recommended improvement.

---

## Visual UI Validation

AccessEase uses browser automation and image-based comparison to identify visual changes between an approved baseline and a current website rendering.

The system can help identify:

* Layout shifts
* Rendering inconsistencies
* Spacing changes
* Visual differences
* UI regressions

Users can compare saved scans and identify **new issues, fixed issues, and unchanged issues**.

---

## Project Structure

A typical structure of the application is:

```text
AccessEase/
│
├── Controllers/
├── Models/
├── ViewModels/
├── Views/
│   ├── Account/
│   ├── Dashboard/
│   ├── Projects/
│   ├── Scans/
│   ├── Reports/
│   └── ...
│
├── Data/
├── Services/
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── images/
│
├── Migrations/
├── Program.cs
├── appsettings.json
└── README.md
```

> The exact folder structure may vary depending on the current implementation of the repository.

---

## Getting Started

### Prerequisites

Make sure the following are installed:

* .NET SDK
* PostgreSQL
* Visual Studio 2022 / Visual Studio Code
* Git
* Google Chrome or Mozilla Firefox
* ChromeDriver / GeckoDriver

### Clone the Repository

```bash
git clone https://github.com/your-username/AccessEase.git
cd AccessEase
```

### Configure the Database

Update the database connection string in:

```text
appsettings.json
```

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AccessEase;Username=postgres;Password=your_password"
  }
}
```

### Apply Database Migrations

```bash
dotnet ef database update
```

### Run the Application

```bash
dotnet run
```

Then open the local URL provided by ASP.NET Core in your browser.

---

## Testing Flow

A typical testing workflow is:

1. Sign in to AccessEase.
2. Select or create a testing project.
3. Submit the target website URL.
4. Select the required testing mode.
5. Run the automated scan.
6. Review accessibility and/or visual UI results.
7. Compare the result with an approved baseline when applicable.
8. Review new, fixed, or unchanged issues.
9. Track remediation progress.
10. Generate or export the testing report.

---

## Development Methodology

AccessEase was developed using the **Evolutionary Prototyping** methodology.

The development process follows repeated cycles of:

```text
Requirements
     ↓
Initial Design
     ↓
Prototype Development
     ↓
Testing & Validation
     ↓
Feedback
     ↓
Refinement
     ↓
Improved Prototype
     ↺
```

This approach was selected because accessibility validation and visual UI testing require continuous refinement and testing to improve accuracy and usability.

---

## Project Scope

AccessEase is designed as a **web-based application** focused on testing web applications.

The project covers:

* Automated accessibility testing
* WCAG 2.1 validation
* Visual UI testing
* Screenshot comparison
* Scan result management
* Project management
* Reporting
* Baseline management
* Remediation tracking
* Role-based access control
* CI/CD-triggered testing

Mobile application accessibility testing is outside the current project scope.

---

## Academic Project

AccessEase was developed as a Final Year Project for the **Bachelor of Information Technology (Hons) in Software Engineering** at Universiti Kuala Lumpur Malaysian Institute of Information Technology (UniKL MIIT).

### Project Title

**AccessEase: Automated Web Accessibility & UI Testing Platform**

### Authors

**Ahmad Amirul Faiz Bin Nazri**
**Anas Bin Idris**

---

## Disclaimer

AccessEase is an academic software project developed for automated web accessibility and UI testing research and development.

Automated testing should be used as part of a broader quality assurance process. Automated results may not identify every accessibility or usability issue, and manual evaluation may still be required.

---

## License

This project is an academic project. Please contact the authors before reusing, distributing, or modifying substantial portions of the project.

---

## Acknowledgements

Developed as part of the Software Engineering programme at Universiti Kuala Lumpur Malaysian Institute of Information Technology (UniKL MIIT).
