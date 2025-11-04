import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AlertList } from './AlertList';
import * as alertsApi from '../services/alertsApi';
import type { Alert, PaginatedResponse } from '../types/alert';

vi.mock('../services/alertsApi');

const fetchAlertsMock = vi.mocked(alertsApi.fetchAlerts);
const acknowledgeAlertMock = vi.mocked(alertsApi.acknowledgeAlert);

const mockAlerts: Alert[] = [
  {
    id: '1',
    ruleId: 'rule-1',
    ruleName: 'Test Rule 1',
    severity: 'high',
    status: 'new',
    triggeredAt: '2024-01-01T00:00:00Z',
    source: 'sensor-1',
    context: {},
    matchedConditions: [],
    aggregationCount: null,
    aggregatedValue: null,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
  {
    id: '2',
    ruleId: 'rule-2',
    ruleName: 'Test Rule 2',
    severity: 'critical',
    status: 'acknowledged',
    triggeredAt: '2024-01-02T00:00:00Z',
    source: 'sensor-2',
    context: {},
    matchedConditions: [],
    aggregationCount: null,
    aggregatedValue: null,
    createdAt: '2024-01-02T00:00:00Z',
    updatedAt: '2024-01-02T00:00:00Z',
  },
];

const mockResponse: PaginatedResponse<Alert> = {
  items: mockAlerts,
  page: 1,
  pageSize: 25,
  totalCount: 2,
  totalPages: 1,
};

describe('AlertList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    fetchAlertsMock.mockReset();
    acknowledgeAlertMock.mockReset();
  });

  it('displays loading state initially', () => {
    fetchAlertsMock.mockReturnValue(
      new Promise(() => {})
    );

    render(<AlertList />);

    expect(screen.getByText('Loading alerts...')).toBeInTheDocument();
  });

  it('displays alerts after successful load', async () => {
    fetchAlertsMock.mockResolvedValue(mockResponse);

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('Test Rule 1')).toBeInTheDocument();
    });

    expect(screen.getByText('Test Rule 2')).toBeInTheDocument();
    expect(screen.getByText('high')).toBeInTheDocument();
    expect(screen.getByText('critical')).toBeInTheDocument();
  });

  it('displays error state when fetch fails', async () => {
    fetchAlertsMock.mockRejectedValue(
      new Error('Network error')
    );

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('displays empty state when no alerts', async () => {
    fetchAlertsMock.mockResolvedValue({
      ...mockResponse,
      items: [],
      totalCount: 0,
    });

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('No alerts found')).toBeInTheDocument();
    });
  });

  it('acknowledges alert when button clicked', async () => {
    const acknowledgedAlert = { ...mockAlerts[0], status: 'acknowledged' as const };
    fetchAlertsMock.mockResolvedValue(mockResponse);
    acknowledgeAlertMock.mockResolvedValue(acknowledgedAlert);

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('Test Rule 1')).toBeInTheDocument();
    });

    const acknowledgeButton = screen.getAllByText('Acknowledge')[0];
    await userEvent.click(acknowledgeButton);

    await waitFor(() => {
      expect(acknowledgeAlertMock).toHaveBeenCalledWith('1');
      expect(screen.getAllByText('acknowledged')).toHaveLength(2);
    });

    expect(
      screen.queryByRole('button', {
        name: /acknowledge/i,
      })
    ).toBeNull();
  });

  it('displays source or dash when source is null', async () => {
    const alertsWithNoSource = [{ ...mockAlerts[0], source: null }];
    fetchAlertsMock.mockResolvedValue({
      ...mockResponse,
      items: alertsWithNoSource,
    });

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('-')).toBeInTheDocument();
    });
  });

  it('shows an error when acknowledgement fails', async () => {
    fetchAlertsMock.mockResolvedValue(mockResponse);
    acknowledgeAlertMock.mockRejectedValue(new Error('Failed to acknowledge'));

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('Test Rule 1')).toBeInTheDocument();
    });

    const button = screen.getAllByRole('button', { name: /acknowledge/i })[0];
    await userEvent.click(button);

    await waitFor(() => {
      expect(screen.getByText('Failed to acknowledge')).toBeInTheDocument();
      expect(
        screen.getAllByRole('button', { name: /acknowledge/i })[0]
      ).not.toBeDisabled();
    });
  });

  it('only shows acknowledge button for new alerts', async () => {
    fetchAlertsMock.mockResolvedValue(mockResponse);

    render(<AlertList />);

    await waitFor(() => {
      expect(screen.getByText('Test Rule 1')).toBeInTheDocument();
    });

    const acknowledgeButtons = screen.getAllByRole('button', {
      name: /acknowledge/i,
    });

    expect(acknowledgeButtons).toHaveLength(1);
  });
});
