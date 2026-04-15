# Kontakte_MB — Contact Management for Merkur Privatbank

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3.3-7952B3?logo=bootstrap)](https://getbootstrap.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](https://www.docker.com/)

**Kontakte_MB** is an ASP.NET Core 8 MVC web application for managing contacts and companies. It was built for **Merkur Privatbank KGaA** and features cookie-based authentication, advanced search and filtering, CSV export, data import from IBM Notes, a dashboard with KPIs, dark mode, and a responsive corporate UI.

---

## Table of Contents

- [Features](#features)
- [Screenshots / UI Overview](#screenshots--ui-overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Run Locally](#run-locally)
  - [Run with Docker](#run-with-docker)
  - [Deploy to a Cloud Platform (Railway, etc.)](#deploy-to-a-cloud-platform)
  - [Deploy to IIS (Windows)](#deploy-to-iis-windows)
- [Configuration](#configuration)
  - [Database Provider](#database-provider)
  - [Connection Strings](#connection-strings)
  - [Environment Variables](#environment-variables)
- [Default Credentials](#default-credentials)
- [Data Model](#data-model)
  - [Company](#company)
  - [Contact](#contact)
  - [AppUser](#appuser)
- [Controllers & API Endpoints](#controllers--api-endpoints)
  - [AccountController — Authentication](#accountcontroller--authentication)
  - [HomeController — Dashboard & Import](#homecontroller--dashboard--import)
  - [CompaniesController — Company CRUD](#companiescontroller--company-crud)
  - [ContactsController — Contact CRUD](#contactscontroller--contact-crud)
  - [SearchController — Global Search](#searchcontroller--global-search)
- [Views](#views)
- [ViewModels](#viewmodels)
- [Services](#services)
  - [ImportService](#importservice)
- [Data Layer](#data-layer)
- [Authentication & Authorization](#authentication--authorization)
- [Frontend & Styling](#frontend--styling)
- [Data Import (IBM Notes)](#data-import-ibm-notes)
- [CSV Export](#csv-export)
- [Dark Mode](#dark-mode)
- [License](#license)

---

## Features

| Feature | Description |
|---|---|
| **Company & Contact Management** | Full CRUD (Create, Read, Update, Soft-Delete) for companies and contacts |
| **Dashboard** | KPI cards showing totals, favorites, email/phone stats, recent items |
| **Advanced Search** | Full-text search across multiple fields with per-entity and global search |
| **Filtering** | Filter by city, country, company, email presence, phone presence |
| **Sorting** | Multi-field sorting (name, date, city) with ascending/descending order |
| **Pagination** | 50 items per page with navigation controls |
| **CSV Export** | Download filtered results as UTF-8 CSV files |
| **Favorites** | Star / unstar companies and contacts |
| **Data Import** | Bulk import from `Kontakte_MB.json` (IBM Notes / Lotus Notes export) |
| **Dark Mode** | Client-side toggle with `localStorage` persistence |
| **Responsive Design** | Mobile-friendly layout built with Bootstrap 5 |
| **Soft Deletes** | Logical deletion via `IsDeleted` flag — data is never physically removed |
| **Duplicate Prevention** | Email uniqueness validation on contact creation and editing |
| **Audit Timestamps** | `CreatedAt` / `UpdatedAt` tracked automatically on all entities |
| **Cookie Auth** | Secure cookie-based authentication with SHA-256 password hashing |
| **Docker Support** | Multi-stage Dockerfile for containerized deployment |
| **Cloud Ready** | Respects `PORT` env var, forwarded headers, and reverse-proxy setups |

---

## Screenshots / UI Overview

The application uses a **sidebar navigation layout** with the Merkur Privatbank corporate design (gray tones, clean cards, Bootstrap Icons). Key pages include:

- **Login** — Standalone page with branded form
- **Dashboard** — KPI metric cards, recent companies/contacts, favorites
- **Companies List** — Searchable, filterable, sortable table with pagination
- **Company Details** — Full company info plus associated contacts
- **Contacts List** — Advanced filters (company, city, has-email, has-phone)
- **Contact Details** — All contact fields with linked company
- **Global Search** — Unified results across companies and contacts

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Framework** | ASP.NET Core 8 (MVC + Razor Views) |
| **ORM** | Entity Framework Core 8.0.4 |
| **Database** | SQLite (default) or SQL Server |
| **Authentication** | Cookie-based with claims identity |
| **UI** | Bootstrap 5.3.3, Bootstrap Icons 1.11.3 |
| **Validation** | jQuery Validation + Unobtrusive Validation |
| **Styling** | Custom CSS (Merkur corporate design system) |
| **Containerization** | Docker (multi-stage build, .NET 8 runtime) |
| **Hosting** | IIS (in-process) / Docker / Railway / any cloud |

---

## Project Structure

```
Kontakte_MB/
├── Controllers/
│   ├── AccountController.cs      # Login / Logout
│   ├── CompaniesController.cs    # Company CRUD, search, export
│   ├── ContactsController.cs     # Contact CRUD, search, export
│   ├── HomeController.cs         # Dashboard & data import
│   └── SearchController.cs       # Global search
├── Data/
│   └── AppDbContext.cs            # EF Core DbContext, model config, indexes
├── Migrations/
│   └── *_InitialCreate.cs        # Database schema migration
├── Models/
│   ├── AppUser.cs                # User entity
│   ├── Company.cs                # Company entity
│   ├── Contact.cs                # Contact entity
│   └── ErrorViewModel.cs         # Error display model
├── Services/
│   └── ImportService.cs          # JSON import from IBM Notes
├── ViewModels/
│   ├── CompanyListViewModel.cs   # Company list page model
│   ├── ContactListViewModel.cs   # Contact list page model
│   ├── DashboardViewModel.cs     # Dashboard KPI model
│   ├── LoginViewModel.cs         # Login form model
│   └── SearchViewModel.cs        # Search results model
├── Views/
│   ├── Account/Login.cshtml
│   ├── Companies/{Index,Details,Create,Edit,Delete}.cshtml
│   ├── Contacts/{Index,Details,Create,Edit,Delete}.cshtml
│   ├── Home/{Index,Privacy}.cshtml
│   ├── Search/Index.cshtml
│   └── Shared/{_Layout,_LoginLayout,Error,...}.cshtml
├── wwwroot/
│   ├── css/                      # merkur.css, kontakte.css, site.css
│   ├── js/                       # kontakte.js, site.js
│   ├── images/                   # merkur-logo.svg
│   └── lib/                      # Bootstrap, jQuery, validation plugins
├── Program.cs                    # Application entry point & configuration
├── KontakteDB.csproj             # Project file & NuGet dependencies
├── Kontakte_MB.json              # IBM Notes data export (seed data)
├── Dockerfile                    # Multi-stage Docker build
├── web.config                    # IIS hosting configuration
├── appsettings.json              # App configuration
├── appsettings.Development.json  # Development overrides
├── merkur.css                    # Corporate design stylesheet (source)
└── _LoginLayout.cshtml           # Login page layout (source)
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local development)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

### Run Locally

```bash
# Clone the repository
git clone https://github.com/andershow88/Kontakte_MB.git
cd Kontakte_MB

# Restore NuGet packages
dotnet restore

# Run the application
dotnet run
```

The app starts at:
- **HTTP:** `http://localhost:5243`
- **HTTPS:** `https://localhost:7129`

On first run, the database (`kontakte.db`) is created automatically via EF Core migrations, and a default admin user is seeded.

### Run with Docker

```bash
# Build the Docker image
docker build -t kontakte-mb .

# Run the container
docker run -p 8080:5000 -e PORT=5000 kontakte-mb
```

The app is available at `http://localhost:8080`.

### Deploy to a Cloud Platform

The application is cloud-ready (e.g., Railway, Render, Heroku). It:

- Reads the `PORT` environment variable and binds to `http://0.0.0.0:$PORT`
- Handles `X-Forwarded-For` and `X-Forwarded-Proto` headers for reverse proxies
- Does **not** enforce HTTPS redirect (the proxy/load balancer handles TLS)

### Deploy to IIS (Windows)

The included `web.config` configures:

- **ASP.NET Core Module V2** with in-process hosting
- Production environment (`ASPNETCORE_ENVIRONMENT=Production`)
- Entry point: `KontakteDB.dll`

Publish the app and deploy to an IIS site:

```bash
dotnet publish -c Release -o ./publish
```

---

## Configuration

### Database Provider

Set the `UseDatabase` key in `appsettings.json`:

| Value | Provider | Default |
|---|---|---|
| `"SQLite"` | SQLite (file-based) | ✅ Yes |
| `"SqlServer"` | Microsoft SQL Server | No |

### Connection Strings

```json
{
  "UseDatabase": "SQLite",
  "ConnectionStrings": {
    "SQLite": "Data Source=kontakte.db",
    "SqlServer": "Server=.;Database=KontakteDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

When using SQLite (default), the database file is stored at the application's content root.

### Environment Variables

| Variable | Purpose | Example |
|---|---|---|
| `PORT` | HTTP listen port (cloud deployments) | `8080` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development`, `Production` |

---

## Default Credentials

On first startup (empty database), the following admin user is created automatically:

| Field | Value |
|---|---|
| **Username** | `admin` |
| **Password** | `Admin1234!` |
| **Role** | `Admin` |

> ⚠️ **Change the default password immediately after first login in a production environment.**

---

## Data Model

### Company

| Property | Type | Constraints |
|---|---|---|
| `Id` | `int` | Primary Key, auto-increment |
| `Name` | `string` | Required, max 300 chars, indexed |
| `Street` | `string?` | Max 300 |
| `ZipCode` | `string?` | Max 20 |
| `City` | `string?` | Max 150, indexed |
| `Country` | `string?` | Max 100 |
| `Phone` | `string?` | Max 100 |
| `Email` | `string?` | Max 200, validated |
| `Website` | `string?` | Max 300 |
| `Industry` | `string?` | Max 100 |
| `Notes` | `string?` | Max 2000 |
| `IsFavorite` | `bool` | Default `false` |
| `CreatedAt` | `DateTime` | UTC, indexed |
| `UpdatedAt` | `DateTime` | UTC |
| `IsDeleted` | `bool` | Soft delete flag |

**Relationships:** A company has many contacts (`1:N`).

### Contact

| Property | Type | Constraints |
|---|---|---|
| `Id` | `int` | Primary Key, auto-increment |
| `CompanyId` | `int?` | Foreign Key → Company, indexed, `SetNull` on delete |
| `Salutation` | `string?` | Max 50 |
| `Title` | `string?` | Max 100 |
| `FirstName` | `string?` | Max 150 |
| `LastName` | `string` | Required, max 200, indexed |
| `Position` | `string?` | Max 200 |
| `Department` | `string?` | Max 200 |
| `Email` | `string?` | Max 200, indexed, validated |
| `Phone` | `string?` | Max 100 |
| `Mobile` | `string?` | Max 100 |
| `Fax` | `string?` | Max 100 |
| `Street` | `string?` | Max 300 |
| `ZipCode` | `string?` | Max 20 |
| `City` | `string?` | Max 150 |
| `Country` | `string?` | Max 100 |
| `PreferredGreeting` | `string?` | Max 100 |
| `Notes` | `string?` | Max 2000 |
| `IsFavorite` | `bool` | Default `false` |
| `CreatedAt` | `DateTime` | UTC |
| `UpdatedAt` | `DateTime` | UTC |
| `IsDeleted` | `bool` | Soft delete flag |

**Relationships:** A contact optionally belongs to one company (`N:1`). If the company is deleted, `CompanyId` is set to `null`.

### AppUser

| Property | Type | Constraints |
|---|---|---|
| `Id` | `int` | Primary Key, auto-increment |
| `Username` | `string` | Required, max 100, unique index |
| `PasswordHash` | `string` | Required, max 200 (SHA-256) |
| `DisplayName` | `string?` | Max 200 |
| `Email` | `string?` | Max 200 |
| `Role` | `string` | Max 50, default `"Admin"` |
| `IsActive` | `bool` | Default `true` |
| `CreatedAt` | `DateTime` | UTC |

---

## Controllers & API Endpoints

All endpoints (except login) require authentication. The application uses **cookie-based authentication** with CSRF protection.

### AccountController — Authentication

| Route | Method | Auth | Description |
|---|---|---|---|
| `/Account/Login` | GET | Public | Display login form |
| `/Account/Login` | POST | Public | Validate credentials and issue auth cookie |
| `/Account/Logout` | POST | Required | Clear auth cookie and redirect to login |

### HomeController — Dashboard & Import

| Route | Method | Auth | Description |
|---|---|---|---|
| `/` or `/Home` | GET | Required | Dashboard with KPIs, recent items, favorites |
| `/Home/Import` | POST | Required | Import data from `Kontakte_MB.json` |

**Dashboard metrics include:** total companies, total contacts, favorite counts, contacts with email, contacts with phone, 5 recent companies, 8 recent contacts, 6 favorite companies.

### CompaniesController — Company CRUD

| Route | Method | Auth | Description |
|---|---|---|---|
| `/Companies` | GET | Required | List with search, filter, sort, pagination |
| `/Companies/Details/{id}` | GET | Required | View company + associated contacts |
| `/Companies/Create` | GET | Required | Create form |
| `/Companies/Create` | POST | Required | Save new company |
| `/Companies/Edit/{id}` | GET | Required | Edit form |
| `/Companies/Edit/{id}` | POST | Required | Update company |
| `/Companies/Delete/{id}` | GET | Required | Delete confirmation |
| `/Companies/Delete/{id}` | POST | Required | Soft-delete company |
| `/Companies/ToggleFavorite/{id}` | POST | Required | Toggle favorite status |
| `/Companies/ExportCsv` | GET | Required | Export filtered results to CSV |

**Search fields:** Name, City, Email, Phone, Notes.
**Filter options:** City, Country.
**Sort options:** Name, City, UpdatedAt, CreatedAt (asc/desc).

### ContactsController — Contact CRUD

| Route | Method | Auth | Description |
|---|---|---|---|
| `/Contacts` | GET | Required | List with advanced filters, sort, pagination |
| `/Contacts/Details/{id}` | GET | Required | View contact details |
| `/Contacts/Create` | GET | Required | Create form with company selector |
| `/Contacts/Create` | POST | Required | Save new contact (checks email duplicates) |
| `/Contacts/Edit/{id}` | GET | Required | Edit form |
| `/Contacts/Edit/{id}` | POST | Required | Update contact (checks email duplicates) |
| `/Contacts/Delete/{id}` | GET | Required | Delete confirmation |
| `/Contacts/Delete/{id}` | POST | Required | Soft-delete contact |
| `/Contacts/ToggleFavorite/{id}` | POST | Required | Toggle favorite status |
| `/Contacts/ExportCsv` | GET | Required | Export filtered results to CSV |

**Search fields:** LastName, FirstName, Email, Phone, Mobile, Position, City, Notes, Company Name.
**Filter options:** Company, City, Country, has email, has phone.
**Sort options:** LastName, FirstName, Company, UpdatedAt, CreatedAt (asc/desc).

### SearchController — Global Search

| Route | Method | Auth | Description |
|---|---|---|---|
| `/Search` | GET | Required | Search across companies (max 30) and contacts (max 50) |

---

## Views

The application uses **Razor Views** with two layout templates:

| Layout | Used by |
|---|---|
| `_Layout.cshtml` | All authenticated pages (sidebar + topbar + content) |
| `_LoginLayout.cshtml` | Login page (standalone centered form) |

**View directory structure:**

```
Views/
├── Account/
│   └── Login.cshtml                # Login form
├── Companies/
│   ├── Index.cshtml                # Company list (search, filter, pagination)
│   ├── Details.cshtml              # Company detail + associated contacts
│   ├── Create.cshtml               # Company creation form
│   ├── Edit.cshtml                 # Company edit form
│   └── Delete.cshtml               # Delete confirmation
├── Contacts/
│   ├── Index.cshtml                # Contact list (advanced filters)
│   ├── Details.cshtml              # Contact detail view
│   ├── Create.cshtml               # Contact creation form
│   ├── Edit.cshtml                 # Contact edit form
│   └── Delete.cshtml               # Delete confirmation
├── Home/
│   ├── Index.cshtml                # Dashboard with KPIs
│   └── Privacy.cshtml              # Privacy policy page
├── Search/
│   └── Index.cshtml                # Global search results
└── Shared/
    ├── _Layout.cshtml              # Main layout (sidebar, nav, content area)
    ├── _LoginLayout.cshtml         # Login layout (standalone)
    ├── _ValidationScriptsPartial.cshtml  # jQuery validation scripts
    └── Error.cshtml                # Error page
```

---

## ViewModels

| ViewModel | Purpose |
|---|---|
| `DashboardViewModel` | KPI metrics, recent companies/contacts, favorite lists |
| `LoginViewModel` | Login form fields (`Benutzername`, `Passwort`, `ReturnUrl`) |
| `CompanyListViewModel` | Company list with search term, filters, sort, pagination, dropdown data |
| `ContactListViewModel` | Contact list with search term, filters (company, city, has-email, has-phone), sort, pagination |
| `SearchViewModel` | Global search query, matched companies & contacts, result counts |

---

## Services

### ImportService

**File:** `Services/ImportService.cs`

The `ImportService` parses `Kontakte_MB.json` — a structured export from **IBM Notes (Lotus Notes)** — and imports companies and contacts into the database.

**Key behavior:**

- Maps JSON fields to EF Core entities
- Handles company-associated contacts and standalone contacts
- Truncates field values to prevent database overflow errors
- Extracts phone, mobile, fax, and website from the `contact_methods` array
- Falls back to company address when a contact has no address
- Logs warnings for skipped records (non-fatal errors)
- Returns an `ImportResult` with counts and error details

**`ImportResult` properties:**

| Property | Type | Description |
|---|---|---|
| `CompaniesImported` | `int` | Number of companies imported |
| `ContactsImported` | `int` | Number of contacts imported |
| `Errors` | `int` | Number of non-fatal errors |
| `Error` | `string?` | Fatal error message (if any) |
| `Success` | `bool` | `true` if no fatal error occurred |

---

## Data Layer

**File:** `Data/AppDbContext.cs`

The `AppDbContext` extends `DbContext` and configures:

| DbSet | Entity | Description |
|---|---|---|
| `Companies` | `Company` | All companies |
| `Contacts` | `Contact` | All contacts |
| `Users` | `AppUser` | Application users |

**Key configuration:**

- **Soft delete query filters:** `!IsDeleted` applied globally to Company and Contact queries
- **Indexes:** `Company.Name`, `Company.City`, `Contact.LastName`, `Contact.Email`, `Contact.CompanyId`, `AppUser.Username` (unique)
- **Cascade behavior:** Deleting a company sets `CompanyId = null` on associated contacts (`SetNull`)

---

## Authentication & Authorization

| Aspect | Details |
|---|---|
| **Scheme** | Cookie authentication (`CookieAuthenticationDefaults`) |
| **Password hashing** | SHA-256 |
| **Login path** | `/Account/Login` |
| **Session duration** | 7 days with sliding expiration |
| **Cookie settings** | `HttpOnly`, `SameSiteMode.Lax` |
| **Access denied** | Redirects to login page |
| **CSRF protection** | `[ValidateAntiForgeryToken]` on all POST actions |
| **Default admin** | `admin` / `Admin1234!` (created on first startup) |

---

## Frontend & Styling

The UI is built with:

- **Bootstrap 5.3.3** — responsive grid, components, utilities
- **Bootstrap Icons 1.11.3** — SVG icon library
- **jQuery + jQuery Validation** — client-side form validation
- **Custom CSS** (`merkur.css`) — Merkur Privatbank corporate design

**Corporate design system highlights:**

| Token | Value | Description |
|---|---|---|
| `--mc-primary` | `#6b6b6b` | Corporate gray |
| `--mc-primary-dark` | `#4a4a4a` | Dark gray |
| `--mc-bg` | `#f4f4f4` | Page background |
| `--mc-sidebar-w` | `268px` | Sidebar width |
| `--mc-topbar-h` | `64px` | Top bar height |
| `--mc-radius` | `10px` | Card border radius |

**Layout:** Fixed sidebar (left) + scrollable main content area with a top bar.

---

## Data Import (IBM Notes)

The application includes a one-click import feature that loads data from `Kontakte_MB.json`.

**Source format:** IBM Notes (Lotus Notes) Structured Text export, converted to JSON.

**Data statistics (included file):**

| Metric | Count |
|---|---|
| Companies | 674 |
| Standalone contacts | 23 |
| Total unique records | 1,492 |
| Duplicates removed | 146 |

**To import:** Log in and click the **Import** button on the dashboard. The import is idempotent — it adds new records each time it runs.

---

## CSV Export

Both the Companies and Contacts list pages offer a **CSV Export** button that:

- Exports the **currently filtered** dataset (respecting active search, filters, and sort)
- Generates a UTF-8 encoded CSV file
- Includes all relevant fields for the entity type
- Downloads directly in the browser

---

## Dark Mode

The application supports a **dark mode** toggle:

- Activated via a toggle button in the UI
- Theme preference is stored in `localStorage` as `mc-theme` (`"light"` or `"dark"`)
- Applied via the `data-theme="dark"` attribute on the `<html>` element
- Persists across page loads and sessions
- Pre-applied before page render to prevent flashing

---

## License

This project is proprietary software developed for **Merkur Privatbank KGaA**.