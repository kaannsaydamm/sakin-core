export type SeverityLevel = 'low' | 'medium' | 'high' | 'critical';
export type AlertStatus = 'new' | 'acknowledged' | 'resolved';

export interface Alert {
  id: string;
  ruleId: string;
  ruleName: string;
  severity: SeverityLevel;
  status: AlertStatus;
  triggeredAt: string;
  source: string | null;
  context: Record<string, unknown>;
  matchedConditions: string[];
  aggregationCount: number | null;
  aggregatedValue: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
