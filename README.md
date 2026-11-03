# Scholar Suite

A complete school management platform — admissions to alumni, academics, attendance, examinations,
fees and a double-entry ledger, HR and payroll, communication, library, transport, inventory and more —
with dedicated portals for administrators, teachers, families and students.

> "Scholar Suite" is a working name. Product branding is configuration (`Platform:ProductName`,
> `NEXT_PUBLIC_PRODUCT_NAME`); each school brings its own name, crest and accent colour.

## Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10 (minimal APIs), C#, EF Core 10, Npgsql, ASP.NET Core Identity, FluentValidation, Serilog, OpenAPI/Swagger |
| Database | PostgreSQL 16 |
| Web | Next.js 16, React 19, TypeScript (strict), Tailwind CSS 4, Radix UI, TanStack Query & Table, React Hook Form + Zod, Recharts, Lucide |
| Tests | xUnit, WebApplicationFactory against real PostgreSQL |

## Documentation

- [Product requirements](docs/PRODUCT_REQUIREMENTS.md)
- [System architecture](docs/SYSTEM_ARCHITECTURE.md)
- [Database design](docs/DATABASE_DESIGN.md)
- [Modules and workflows](docs/MODULES_AND_WORKFLOWS.md)
- [Implementation plan](docs/IMPLEMENTATION_PLAN.md)
- [Progress](docs/PROGRESS.md)

## Getting started

Setup instructions are added as the backend and frontend land (see *Development* below once available).
