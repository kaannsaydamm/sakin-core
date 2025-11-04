# Sakin Panel

## Overview
Web-based user interface and dashboard for the Sakin security platform.

## Purpose
This directory will contain the frontend application that provides:
- Real-time security dashboard
- Alert and incident management interface
- Threat visualization and reporting
- System configuration and administration
- User and access management
- Security analytics and metrics

## Status
🚧 **Placeholder** - Currently exists as a separate repository.

The Sakin Panel is currently maintained as a standalone project at:
[https://github.com/kaannsaydamm/sakin-panel](https://github.com/kaannsaydamm/sakin-panel)

This placeholder is reserved for future mono-repo integration. The monorepo now
contains an ASP.NET Core backend (`Sakin.Panel.Api`) that exposes alert listing
and acknowledgement endpoints backed by the correlation alerts persistence layer.

### Backend API
Run the API with:
```bash
cd sakin-panel/Sakin.Panel.Api
dotnet run
```

Default endpoints:
- `GET /api/alerts` – paginated alert listing with optional severity filter
- `POST /api/alerts/{id}/acknowledge` – acknowledges the specified alert

## Current Setup (External Repository)
```bash
git clone https://github.com/kaannsaydamm/sakin-panel.git
cd sakin-panel
npm install
npm run build
npm run start
```

Access the panel at: `http://localhost:3000`

## Planned Features
- Real-time event streaming and visualization
- Interactive threat timeline
- Network topology mapping
- Incident investigation tools
- SOAR playbook management UI
- Custom dashboard creation
- Reporting and export capabilities
- Multi-tenancy support

## Technology Stack (Planned)
- Modern JavaScript framework (React/Next.js/Vue.js)
- WebSocket for real-time updates
- RESTful API integration
- Authentication and authorization (OAuth2/JWT)
