# Sakin Panel

## Overview
Web-based user interface and dashboard for the Sakin security platform. The
monorepo now ships both the alert management API and a lightweight frontend for
reviewing and acknowledging alerts.

## Projects

- `Sakin.Panel.Api/` &mdash; ASP.NET Core Web API that surfaces correlation alerts
  and acknowledgement endpoints.
- `ui/` &mdash; React + TypeScript single-page application powered by Vite that
  consumes the alert API.

## Running Locally

### Backend API
Run the API with:

```bash
cd sakin-panel/Sakin.Panel.Api
dotnet run
```

Default endpoints:
- `GET /api/alerts` – paginated alert listing with optional severity filter
- `POST /api/alerts/{id}/acknowledge` – acknowledges the specified alert

The API listens on `http://localhost:5000` by default. Swagger UI is available
at `http://localhost:5000/swagger` in development builds.

### Frontend UI
Launch the alert list UI:

```bash
cd sakin-panel/ui
npm install
npm run dev
```

The panel opens on `http://localhost:3000`. Requests to `/api` are proxied to the
backend during development. To change the API origin, set
`VITE_API_BASE_URL=http://hostname:port/api` in an `.env.local` file. See
[`ui/README.md`](./ui/README.md) for additional commands.

## Acceptance Criteria Coverage

- ✅ Lists alerts with severity, rule, source, and timestamp details
- ✅ Acknowledge button calls the acknowledgement endpoint and updates UI state
- ✅ Loading and error states for data fetching and acknowledgement failures
- ✅ Vitest + React Testing Library coverage of primary UI flows

## Planned Enhancements

- Real-time event streaming and visualization
- Advanced filtering, search, and grouping
- Incident investigation workflow
- SOAR playbook management UI
- Custom dashboards and reporting
- Authentication and multi-tenancy support

## Technology Stack

- React 18 + TypeScript (Vite)
- ASP.NET Core 8 Web API
- Entity Framework Core for persistence
- Vitest & React Testing Library for UI tests
