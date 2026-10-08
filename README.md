# AcxiomCRM

AcxiomCRM is a production-quality, role-based Customer Relationship Management (CRM) web application designed for enterprise customer, lead, and sales pipeline management.

Built with **ASP.NET Core 8 MVC**, **ASP.NET Core Identity**, and **Entity Framework Core**, backed by **PostgreSQL**.

---

## Key Modules & Features

- **Authentication & Security**:
  - Secure authentication via ASP.NET Core Identity.
  - PBKDF2 adaptive password hashing and strict password complexity rules.
  - Automated account lockout after consecutive failed attempts.
  - Anti-forgery validation (`[ValidateAntiForgeryToken]`) on all state-changing endpoints.
  - Server-side role authorization (`Admin`, `Manager`, `Sales Executive`).

- **Dashboard**:
  - Real-time calculated KPI metrics (Total Customers, Total Leads, Open Opportunities, Won/Lost Outcomes, Gross Pipeline Value, Probability-Weighted Pipeline).
  - Interactive Chart.js visualizations for Lead Status Distribution, Pipeline Stages, and Monthly Won Sales.
  - Role-scoped data visibility.

- **Customer Management**:
  - Account master records with contact and company information.
  - Email and phone uniqueness enforcement to prevent duplicate account creation.
  - Full activity and audit history tracking.

- **Lead Management & Conversion**:
  - Capture, qualification, and sales representative assignment.
  - Status progression (`New`, `Contacted`, `Qualified`, `Unqualified`, `Converted`, `Lost`).
  - End-to-end Lead Conversion workflow that generates linked Customer accounts and Opportunity records.

- **Opportunity Management**:
  - Pipeline stage progression (`Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`).
  - Strict server-side business rules: positive deal amounts, 0–100% win probabilities, and active close date validation.
  - Automatic probability-weighted value derivation.

- **Follow-Up Management**:
  - Task scheduling with automated checks preventing past-date scheduling for new activities.
  - Complete, mark missed, and reschedule workflows with history tracking.

- **Activity Management**:
  - Logging for Calls, Meetings, Emails, and Tasks linked to CRM records.

- **User & Role Administration**:
  - Admin management for users, role assignments, activation toggles, lockout releases, and password resets.

- **Audit Logging**:
  - Append-only audit trail logging user, timestamp, action, module, result, IP address, and payload diffs.

- **Reports**:
  - Real-time database reports for Customers, Leads, Follow-Ups, Opportunities, Pipeline, Win/Loss Conversion, and User Activities.

- **REST APIs**:
  - Clean DTO-backed RESTful endpoints for external and headless integration with status code compliance.

---

## Technology Stack

- **Framework**: .NET 8.0 (C# / ASP.NET Core MVC)
- **Database Engine**: PostgreSQL
- **Data Access**: Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Identity & Security**: ASP.NET Core Identity
- **UI & Layout**: Semantic HTML5, Bootstrap 5, Bootstrap Icons, Chart.js

---

## Configuration & Deployment

Database connections and sensitive secrets are loaded via environment variables or configuration providers.

```bash
# Set PostgreSQL connection string in production
export ConnectionStrings__DefaultConnection="Host=your_postgres_host;Port=5432;Database=acxiomcrm_db;Username=your_user;Password=your_password"
```

To run locally:
```bash
dotnet restore
dotnet build
dotnet run
```
