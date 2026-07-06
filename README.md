# Fridge Management System

A comprehensive web application built with ASP.NET Core MVC for managing commercial fridge allocations, fault reporting, maintenance scheduling, and the procurement lifecycle. The system connects **Administrators, Customers, Technicians, Purchasing Managers, and Suppliers** through role‑based dashboards, automated notifications, and streamlined workflows.

## Features

- **Role‑based Access** – Distinct interfaces for Administrators, Customer Liaisons, Fault Technicians, Maintenance Technicians, Purchasing Managers, Suppliers, and Customers.
- **Customer & Employee Management** – Full CRUD with soft deletes; automatic creation of linked user accounts with temporary passwords and welcome notifications.
- **Fridge Allocation** – Assign fridges to customers, track allocation history, and return allocations.
- **Fault Reporting & Resolution** – Customers report faults; technicians can assign, schedule, diagnose, repair, and close faults. Priority levels and status tracking.
- **Maintenance Scheduling** – Plan preventative maintenance, assign technicians, complete service checklists, and view maintenance history. Create faults directly from maintenance inspections.
- **Procurement Pipeline**  
  - Purchase requests → Approval/Rejection  
  - Request for Quotation (RFQ) sent to selected suppliers  
  - Suppliers submit quotations; purchasing manager accepts the best quote  
  - Automatic purchase order creation  
  - Delivery notes to track shipments and update order status
- **Notification Engine** – In‑app notifications for key events (new allocations, account creation, quotation acceptance, etc.) with real‑time unread count and mark‑as‑read functionality.
- **Soft Deletes** – All major entities support soft delete and restore, preserving historical data.
- **Supplier Portal** – Suppliers log in to view RFQs, submit quotations, track purchase orders, and create delivery notes.
- **Responsive UI** – Built with Bootstrap for a clean, mobile‑friendly interface.

## User Roles & Permissions

| Role                     | Responsibilities                                                                   |
|--------------------------|-----------------------------------------------------------------------------------|
| **Administrator**        | Manage customers, employees, locations, fridges, and suppliers. Create and restore records. |
| **Customer**             | View allocated fridges, report faults, request new fridges, view upcoming maintenance, and view maintenance history. |
| **Customer Liaison**     | Oversee customer details, allocate fridges to customers, and process returns.     |
| **Fault Technician**     | View all faults, assign technicians, schedule repairs, record diagnostics, complete repairs, and close faults. |
| **Maintenance Technician** | Create and manage maintenance schedules, perform service checklists, log maintenance, raise faults found during maintenance, and view history. |
| **Purchasing Manager**   | Manage suppliers, purchase requests, RFQs, quotations, purchase orders, and delivery processing. |
| **Supplier**             | Respond to RFQs by submitting quotations, track purchase orders, create delivery notes, and manage own profile. |

## Technology Stack

- **Framework**: ASP.NET Core MVC (.NET 6/7/8)
- **ORM**: Entity Framework Core
- **Database**: SQL Server (LocalDB, Express, or full edition)
- **Frontend**: Razor Views, Bootstrap 5, jQuery, AJAX
- **Authentication**: ASP.NET Core Identity with roles (cookie‑based)
- **Notifications**: Custom notification service with in‑app storage
- **File Storage**: Local file system (extendable to cloud)

## Setup & Installation

### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) (6.0 or later)
- SQL Server or SQL Server Express/LocalDB
- Visual Studio 2022 / VS Code / Rider (optional)

### Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/fridge-management.git
   cd fridge-management