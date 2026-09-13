export interface User {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  createdAtUtc: string;
}

export interface AuthResponse {
  user: User;
  accessToken: string;
  refreshToken: string;
}

export interface Employee {
  employeeId: string;
  personNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  legalEmployer: string;
  positionId: string;
  job: string;
  grade: string;
  jobTitle: string;
  department: string;
  workLocation: string | null;
  supervisor: string | null;
  employmentType: string | null;
  payBasis: string | null;
  startDate: string;
  baseSalary: number;
  currency: string;
  isActive: boolean;
  terminationDate: string | null;
  terminationReason: string | null;
  updatedAtUtc: string;
}

export interface Position {
  positionId: string;
  job: string;
  grade: string;
  jobTitle: string;
  department: string;
  location: string | null;
  supervisor: string | null;
  createdAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditEntry {
  id: number;
  atUtc: string;
  source: string;
  eventType: string;
  employeeId: string | null;
  status: string;
  message: string | null;
}

export interface EmployeeChangeField {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface EmployeeChange {
  eventId: string;
  atUtc: string;
  eventType: string;
  fields: EmployeeChangeField[];
}

export interface Health {
  status: string;
  database: boolean;
  lastIngestAtUtc: string | null;
  queueDepth: number;
  timestampUtc: string;
}

export interface Metrics {
  eventsTotal: number;
  applied: number;
  rejected: number;
  errors: number;
  deadLetterPending: number;
  eventsLastMinute: number;
  queueDepth: number;
  lastIngestAtUtc: string | null;
  timestampUtc: string;
}

export interface DeadLetter {
  id: number;
  eventId: string;
  eventType: string;
  employeeId: string | null;
  reason: string;
  status: string; // Pending | Replayed | Discarded
  createdAtUtc: string;
  replayedAtUtc: string | null;
  lastResult: string | null;
}
