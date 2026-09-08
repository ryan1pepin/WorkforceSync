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
  firstName: string;
  lastName: string;
  email: string;
  positionId: string;
  jobTitle: string;
  department: string;
  startDate: string;
  baseSalary: number;
  currency: string;
  isActive: boolean;
  terminationDate: string | null;
  updatedAtUtc: string;
}

export interface Position {
  positionId: string;
  jobTitle: string;
  department: string;
  location: string | null;
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
