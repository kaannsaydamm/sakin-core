import { useCallback, useEffect, useState } from 'react';
import type { Alert } from '../types/alert';
import { fetchAlerts, acknowledgeAlert } from '../services/alertsApi';
import './AlertList.css';

interface AlertListProps {
  pageSize?: number;
}

export function AlertList({ pageSize = 25 }: AlertListProps) {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [acknowledgingIds, setAcknowledgingIds] = useState<Set<string>>(new Set());

  const loadAlerts = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await fetchAlerts(page, pageSize);
      setAlerts(response.items);
      setTotalPages(response.totalPages);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load alerts');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => {
    loadAlerts();
  }, [loadAlerts]);

  async function handleAcknowledge(id: string) {
    setError(null);

    try {
      setAcknowledgingIds((prev) => {
        const next = new Set(prev);
        next.add(id);
        return next;
      });

      const updatedAlert = await acknowledgeAlert(id);
      setAlerts((prev) =>
        prev.map((alert) => (alert.id === id ? updatedAlert : alert))
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to acknowledge alert');
    } finally {
      setAcknowledgingIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
    }
  }

  function formatTimestamp(timestamp: string): string {
    return new Date(timestamp).toLocaleString();
  }

  function getSeverityColor(severity: string): string {
    switch (severity) {
      case 'critical':
        return 'severity-critical';
      case 'high':
        return 'severity-high';
      case 'medium':
        return 'severity-medium';
      case 'low':
        return 'severity-low';
      default:
        return '';
    }
  }

  if (loading && alerts.length === 0) {
    return (
      <div className="alert-list-container">
        <div className="loading-state">
          <div className="spinner" />
          <p>Loading alerts...</p>
        </div>
      </div>
    );
  }

  if (error && alerts.length === 0) {
    return (
      <div className="alert-list-container">
        <div className="error-state">
          <p className="error-message">{error}</p>
          <button className="btn btn-primary" onClick={loadAlerts}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="alert-list-container">
      <div className="alert-list-header">
        <h1>Security Alerts</h1>
        <button
          className="btn btn-secondary"
          onClick={loadAlerts}
          disabled={loading}
        >
          {loading ? 'Refreshing...' : 'Refresh'}
        </button>
      </div>

      {error && (
        <div className="error-banner">
          <p>{error}</p>
          <button className="close-btn" onClick={() => setError(null)}>
            ×
          </button>
        </div>
      )}

      {alerts.length === 0 ? (
        <div className="empty-state">
          <p>No alerts found</p>
        </div>
      ) : (
        <>
          <div className="alert-table">
            <table>
              <thead>
                <tr>
                  <th>Severity</th>
                  <th>Rule</th>
                  <th>Source</th>
                  <th>Timestamp</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {alerts.map((alert) => (
                  <tr key={alert.id} className={`alert-row ${alert.status}`}>
                    <td>
                      <span className={`severity-badge ${getSeverityColor(alert.severity)}`}>
                        {alert.severity}
                      </span>
                    </td>
                    <td>
                      <div className="rule-info">
                        <div className="rule-name">{alert.ruleName}</div>
                        <div className="rule-id">{alert.ruleId}</div>
                      </div>
                    </td>
                    <td>{alert.source || '-'}</td>
                    <td>{formatTimestamp(alert.triggeredAt)}</td>
                    <td>
                      <span className={`status-badge status-${alert.status}`}>
                        {alert.status}
                      </span>
                    </td>
                    <td>
                      {alert.status === 'new' && (
                        <button
                          className="btn btn-sm btn-primary"
                          onClick={() => handleAcknowledge(alert.id)}
                          disabled={acknowledgingIds.has(alert.id)}
                        >
                          {acknowledgingIds.has(alert.id) ? 'Acknowledging...' : 'Acknowledge'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="pagination">
              <button
                className="btn btn-secondary"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1 || loading}
              >
                Previous
              </button>
              <span className="page-info">
                Page {page} of {totalPages}
              </span>
              <button
                className="btn btn-secondary"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages || loading}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
