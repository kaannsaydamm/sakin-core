# Sakin Panel UI

Web interface for the Sakin security platform, providing real-time alert monitoring and management.

## Features

- 📊 Alert list with pagination
- ✅ Acknowledge alerts with one click
- 🔄 Loading and error state handling
- 📱 Responsive design

## Tech Stack

- **React 18** with TypeScript
- **Vite** for fast development and builds
- **Vitest** for unit testing
- **CSS** for styling

## Prerequisites

- Node.js 18+ and npm/yarn/pnpm
- Running Sakin Panel API backend (default: `http://localhost:5000`)

## Getting Started

### Install Dependencies

```bash
npm install
```

### Development

Start the development server:

```bash
npm run dev
```

The UI will be available at `http://localhost:3000`.

### Configuration

The UI proxies API requests to the backend. By default, it targets `http://localhost:5000/api`.

To override the API base URL, set the `VITE_API_BASE_URL` environment variable:

```bash
# .env.local
VITE_API_BASE_URL=http://localhost:5000/api
```

### Build for Production

```bash
npm run build
```

Output will be in the `dist/` directory.

### Preview Production Build

```bash
npm run preview
```

## Testing

Run unit tests:

```bash
npm test
```

Run tests in watch mode (during development):

```bash
npm test -- --watch
```

## Project Structure

```
ui/
├── src/
│   ├── components/
│   │   ├── AlertList.tsx          # Main alert list component
│   │   ├── AlertList.css          # Alert list styles
│   │   └── AlertList.test.tsx     # Alert list tests
│   ├── services/
│   │   └── alertsApi.ts           # API client for alerts
│   ├── types/
│   │   └── alert.ts               # TypeScript type definitions
│   ├── test/
│   │   └── setup.ts               # Test configuration
│   ├── App.tsx                    # Root application component
│   ├── App.css                    # Application styles
│   ├── main.tsx                   # Application entry point
│   └── index.css                  # Global styles
├── index.html
├── vite.config.ts
├── tsconfig.json
└── package.json
```

## Components

### AlertList

Main component that displays a paginated list of security alerts.

**Props:**
- `pageSize?: number` - Number of alerts per page (default: 25)

**Features:**
- Displays alert details: severity, rule, source, timestamp, status
- Acknowledge button for new alerts
- Pagination controls
- Loading spinner
- Error handling with retry
- Empty state

## API Integration

The UI communicates with the Sakin Panel API:

- `GET /api/alerts?page=1&pageSize=25` - Fetch paginated alerts
- `POST /api/alerts/{id}/acknowledge` - Acknowledge an alert

## Development Guidelines

- Use TypeScript for type safety
- Follow React hooks best practices
- Write unit tests for new components
- Use CSS for styling (no external CSS frameworks)
- Keep components simple and focused

## Troubleshooting

### CORS Issues

If you encounter CORS errors, ensure the backend API is configured to allow requests from `http://localhost:3000`.

### API Connection Issues

Verify the backend API is running and accessible at the configured URL. Check browser network tab for failed requests.

## License

Proprietary - Part of the Sakin security platform
