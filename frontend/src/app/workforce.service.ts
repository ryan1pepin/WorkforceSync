import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditEntry, Employee, Health, PagedResult, Position } from './models';

@Injectable({ providedIn: 'root' })
export class WorkforceService {
  private http = inject(HttpClient);

  employees(page = 1, pageSize = 50): Observable<PagedResult<Employee>> {
    return this.http.get<PagedResult<Employee>>('/employees', {
      params: { page: String(page), pageSize: String(pageSize) },
    });
  }

  positions(): Observable<Position[]> {
    return this.http.get<Position[]>('/positions');
  }

  audit(limit = 50): Observable<AuditEntry[]> {
    return this.http.get<AuditEntry[]>('/integrations/audit', {
      params: { limit: String(limit) },
    });
  }

  health(): Observable<Health> {
    return this.http.get<Health>('/integrations/health');
  }
}
