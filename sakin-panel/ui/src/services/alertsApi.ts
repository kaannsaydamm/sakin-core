import type { Alert, PaginatedResponse } from '../types/alert';

const DEFAULT_API_BASE_URL = 'http://localhost:5000/api';

function resolveBaseUrl(): string {
  const fromEnv = import.meta.env.VITE_API_BASE_URL as string | undefined;
  const base = (fromEnv ?? DEFAULT_API_BASE_URL).trim();
  return base.endsWith('/') ? base.slice(0, -1) : base;
}

export async function fetchAlerts(
  page = 1,
  pageSize = 25
): Promise<PaginatedResponse<Alert>> {
  const baseUrl = resolveBaseUrl();
  const searchParams = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });

  const response = await fetch(`${baseUrl}/alerts?${searchParams.toString()}`);

  if (!response.ok) {
    throw new Error(`Failed to load alerts (${response.status})`);
  }

  const data: PaginatedResponse<Alert> = await response.json();
  return data;
}

export async function acknowledgeAlert(id: string): Promise<Alert> {
  const baseUrl = resolveBaseUrl();
  const response = await fetch(`${baseUrl}/alerts/${id}/acknowledge`, {
    method: 'POST',
  });

  if (!response.ok) {
    throw new Error(`Failed to acknowledge alert (${response.status})`);
  }

  const data: Alert = await response.json();
  return data;
}
