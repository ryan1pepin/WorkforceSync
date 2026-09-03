import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { WorkforceService } from './workforce.service';
import { AuditEntry, Employee, Health } from './models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  template: `
    <div class="min-h-screen bg-slate-50">
      <!-- Header -->
      <header class="bg-white border-b border-slate-200 shadow-sm">
        <div class="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div>
            <h1 class="text-xl font-bold text-slate-800">WorkforceSync</h1>
            <p class="text-sm text-slate-500">Live workforce integration dashboard</p>
          </div>
          <div class="flex items-center gap-4">
            @if (auth.user(); ) {
              <span class="text-sm text-slate-600">
                {{ auth.user()!.firstName }} {{ auth.user()!.lastName }}
              </span>
            }
            <button
              (click)="logout()"
              class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-600 hover:bg-slate-100 transition"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>

      <main class="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <!-- Health + stats -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-5">
            <div class="text-sm text-slate-500">Pipeline</div>
            <div class="mt-1 flex items-center gap-2">
              <span
                class="inline-block w-2.5 h-2.5 rounded-full"
                [class.bg-green-500]="health()?.status === 'Healthy'"
                [class.bg-amber-500]="health()?.status !== 'Healthy'"
              ></span>
              <span class="font-semibold text-slate-800">{{ health()?.status ?? '…' }}</span>
            </div>
            <div class="text-xs text-slate-400 mt-2">
              Last ingest: {{ health()?.lastIngestAtUtc ? (health()!.lastIngestAtUtc | date:'short') : '—' }}
            </div>
          </div>

          <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-5">
            <div class="text-sm text-slate-500">Total headcount</div>
            <div class="text-2xl font-bold text-slate-800 mt-1">{{ employees().length }}</div>
          </div>

          <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-5">
            <div class="text-sm text-slate-500">Active</div>
            <div class="text-2xl font-bold text-green-600 mt-1">{{ activeCount() }}</div>
          </div>

          <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-5">
            <div class="text-sm text-slate-500">Departments</div>
            <div class="text-2xl font-bold text-slate-800 mt-1">{{ departmentCount() }}</div>
          </div>
        </div>

        <!-- Employees -->
        <section class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
            <h2 class="font-semibold text-slate-800">Employees</h2>
            <span class="text-xs text-slate-400">auto-refreshes as the HCM feed publishes</span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-slate-500 border-b border-slate-100">
                  <th class="px-5 py-3 font-medium">Name</th>
                  <th class="px-5 py-3 font-medium">Job title</th>
                  <th class="px-5 py-3 font-medium">Department</th>
                  <th class="px-5 py-3 font-medium">Start date</th>
                  <th class="px-5 py-3 font-medium text-right">Base salary</th>
                  <th class="px-5 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (e of employees(); track e.employeeId) {
                  <tr class="border-b border-slate-50 hover:bg-slate-50">
                    <td class="px-5 py-3">
                      <div class="font-medium text-slate-800">{{ e.firstName }} {{ e.lastName }}</div>
                      <div class="text-xs text-slate-400">{{ e.email }}</div>
                    </td>
                    <td class="px-5 py-3 text-slate-600">{{ e.jobTitle }}</td>
                    <td class="px-5 py-3 text-slate-600">{{ e.department }}</td>
                    <td class="px-5 py-3 text-slate-600">{{ e.startDate | date:'mediumDate' }}</td>
                    <td class="px-5 py-3 text-right text-slate-800 font-medium">
                      {{ e.baseSalary | number }} {{ e.currency }}
                    </td>
                    <td class="px-5 py-3">
                      @if (e.isActive) {
                        <span class="inline-block rounded-full bg-green-100 text-green-700 px-2.5 py-0.5 text-xs font-medium">Active</span>
                      } @else {
                        <span class="inline-block rounded-full bg-red-100 text-red-700 px-2.5 py-0.5 text-xs font-medium">Terminated</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="px-5 py-8 text-center text-slate-400">No employees yet — waiting for the first hire event.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <!-- Audit log -->
        <section class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-slate-100">
            <h2 class="font-semibold text-slate-800">Integration audit log</h2>
          </div>
          <ul class="divide-y divide-slate-50">
            @for (a of audit(); track a.id) {
              <li class="px-5 py-3 flex items-center gap-3 text-sm">
                <span
                  class="inline-block w-2 h-2 rounded-full shrink-0"
                  [class.bg-green-500]="a.status === 'Success'"
                  [class.bg-red-500]="a.status !== 'Success'"
                ></span>
                <span class="font-medium text-slate-700 w-40 shrink-0">{{ a.eventType }}</span>
                <span class="text-slate-500">{{ a.employeeId }}</span>
                <span class="text-slate-400 truncate flex-1">{{ a.message }}</span>
                <span class="text-xs text-slate-400 shrink-0">{{ a.atUtc | date:'shortTime' }}</span>
              </li>
            } @empty {
              <li class="px-5 py-8 text-center text-slate-400">No integration events yet.</li>
            }
          </ul>
        </section>
      </main>
    </div>
  `,
})
export class Dashboard implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  private workforce = inject(WorkforceService);
  private router = inject(Router);

  readonly employees = signal<Employee[]>([]);
  readonly audit = signal<AuditEntry[]>([]);
  readonly health = signal<Health | null>(null);

  readonly activeCount = () => this.employees().filter((e) => e.isActive).length;
  readonly departmentCount = () =>
    new Set(this.employees().map((e) => e.department)).size;

  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.refresh();
    this.timer = setInterval(() => this.refresh(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  private refresh(): void {
    this.workforce.employees().subscribe({
      next: (res) => this.employees.set(res.items),
      error: () => {
        /* interceptor handles 401 → logout; ignore transient errors */
      },
    });
    this.workforce.audit(30).subscribe({
      next: (items) => this.audit.set(items),
      error: () => {
        /* ignore */
      },
    });
    this.workforce.health().subscribe({
      next: (h) => this.health.set(h),
      error: () => {
        /* ignore */
      },
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
