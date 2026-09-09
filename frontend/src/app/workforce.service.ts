import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditEntry, Employee, EmployeeChange, Health, PagedResult, Position } from './models';

@Injectable({ providedIn: 'root' })
export class WorkforceService {
  private http = inject(HttpClient);

  employees(
    page = 1,
    pageSize = 50,
    opts: { status?: string; department?: string; search?: string } = {},
  ): Observable<PagedResult<Employee>> {
    const params: Record<string, string> = { page: String(page), pageSize: String(pageSize) };
    if (opts.status) params['status'] = opts.status;
    if (opts.department) params['department'] = opts.department;
    if (opts.search) params['search'] = opts.search;
    return this.http.get<PagedResult<Employee>>('/employees', { params });
  }

  employeeChanges(employeeId: string): Observable<EmployeeChange[]> {
    return this.http.get<EmployeeChange[]>(`/employees/${employeeId}/changes`);
  }

  positions(): Observable<Position[]> {
    return this.http.get<Position[]>('/positions');
  }

  audit(limit = 50, status?: string, eventType?: string, search?: string): Observable<AuditEntry[]> {
    const params: Record<string, string> = { limit: String(limit) };
    if (status) params['status'] = status;
    if (eventType) params['eventType'] = eventType;
    if (search) params['search'] = search;
    return this.http.get<AuditEntry[]>('/integrations/audit', { params });
  }

  health(): Observable<Health> {
    return this.http.get<Health>('/integrations/health');
  }
}
