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
    <div class="min-h-screen bg-gradient-to-br from-slate-100 via-slate-50 to-blue-50/40">
      <!-- Header -->
      <header class="bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 shadow-lg shadow-slate-900/20">
        <div class="max-w-7xl mx-auto px-6 py-5 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center shadow-md shadow-blue-900/40">
              <span class="text-white font-bold text-lg">W</span>
            </div>
            <div>
              <h1 class="text-xl font-bold text-white tracking-tight">WorkforceSync</h1>
              <p class="text-xs text-slate-400">Live workforce integration dashboard</p>
            </div>
          </div>
          <div class="flex items-center gap-4">
            @if (auth.user(); ) {
              <div class="text-right hidden sm:block">
                <div class="text-sm text-white font-medium">{{ auth.user()!.firstName }} {{ auth.user()!.lastName }}</div>
                <div class="text-xs text-slate-400">{{ auth.user()!.email }}</div>
              </div>
            }
            <button
              (click)="logout()"
              class="rounded-lg bg-white/10 hover:bg-white/20 border border-white/10 px-4 py-2 text-sm text-white transition shadow-sm"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>

      <main class="max-w-7xl mx-auto px-6 py-8 space-y-6">
        <!-- Health + stats -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <div class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 p-5 hover:shadow-xl hover:-translate-y-0.5 transition-all duration-200">
            <div class="flex items-center justify-between">
              <div class="text-sm font-medium text-slate-500">Pipeline</div>
              <span
                class="inline-block w-2.5 h-2.5 rounded-full"
                [class.bg-green-500]="health()?.status === 'Healthy'"
                [class.bg-amber-500]="health()?.status !== 'Healthy'"
              ></span>
            </div>
            <div class="mt-2 text-2xl font-bold text-slate-800">{{ health()?.status ?? '…' }}</div>
            <div class="text-xs text-slate-400 mt-2">
              Last ingest: {{ health()?.lastIngestAtUtc ? (health()!.lastIngestAtUtc | date:'short') : '—' }}
            </div>
          </div>

          <div class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 p-5 hover:shadow-xl hover:-translate-y-0.5 transition-all duration-200">
            <div class="text-sm font-medium text-slate-500">Total headcount</div>
            <div class="mt-2 text-3xl font-bold text-slate-800">{{ employees().length }}</div>
            <div class="text-xs text-slate-400 mt-2">employees in the system</div>
          </div>

          <div class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 p-5 hover:shadow-xl hover:-translate-y-0.5 transition-all duration-200">
            <div class="text-sm font-medium text-slate-500">Active</div>
            <div class="mt-2 text-3xl font-bold text-green-600">{{ activeCount() }}</div>
            <div class="text-xs text-slate-400 mt-2">{{ terminatedCount() }} terminated</div>
          </div>

          <div class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 p-5 hover:shadow-xl hover:-translate-y-0.5 transition-all duration-200">
            <div class="text-sm font-medium text-slate-500">Departments</div>
            <div class="mt-2 text-3xl font-bold text-slate-800">{{ departmentCount() }}</div>
            <div class="text-xs text-slate-400 mt-2">across the org</div>
          </div>
        </div>

        <!-- Employees -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-gradient-to-r from-white to-slate-50/50">
            <div>
              <h2 class="font-semibold text-slate-800">Employees</h2>
              <p class="text-xs text-slate-400">auto-refreshes as the HCM feed publishes</p>
            </div>
            <span class="text-xs font-medium text-blue-600 bg-blue-50 border border-blue-100 rounded-full px-3 py-1">
              {{ employees().length }} records
            </span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-slate-500 border-b border-slate-100 bg-slate-50/50">
                  <th class="px-6 py-3 font-medium">Name</th>
                  <th class="px-6 py-3 font-medium">Job title</th>
                  <th class="px-6 py-3 font-medium">Department</th>
                  <th class="px-6 py-3 font-medium">Start date</th>
                  <th class="px-6 py-3 font-medium text-right">Base salary</th>
                  <th class="px-6 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                @for (e of employees(); track e.employeeId) {
                  <tr class="border-b border-slate-50 hover:bg-blue-50/30 transition-colors">
                    <td class="px-6 py-3.5">
                      <div class="font-medium text-slate-800">{{ e.firstName }} {{ e.lastName }}</div>
                      <div class="text-xs text-slate-400">{{ e.email }}</div>
                    </td>
                    <td class="px-6 py-3.5 text-slate-600">{{ e.jobTitle }}</td>
                    <td class="px-6 py-3.5">
                      <span class="inline-block rounded-md bg-slate-100 text-slate-600 px-2 py-0.5 text-xs font-medium">{{ e.department }}</span>
                    </td>
                    <td class="px-6 py-3.5 text-slate-600">{{ e.startDate | date:'mediumDate' }}</td>
                    <td class="px-6 py-3.5 text-right text-slate-800 font-semibold">
                      {{ e.baseSalary | number }} <span class="text-xs text-slate-400 font-normal">{{ e.currency }}</span>
                    </td>
                    <td class="px-6 py-3.5">
                      @if (e.isActive) {
                        <span class="inline-block rounded-full bg-green-100 text-green-700 px-2.5 py-0.5 text-xs font-semibold">Active</span>
                      } @else {
                        <span class="inline-block rounded-full bg-red-100 text-red-700 px-2.5 py-0.5 text-xs font-semibold">Terminated</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="6" class="px-6 py-10 text-center text-slate-400">No employees yet — waiting for the first hire event.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <!-- Audit log -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 bg-gradient-to-r from-white to-slate-50/50">
            <h2 class="font-semibold text-slate-800">Integration audit log</h2>
            <p class="text-xs text-slate-400">every event the pipeline applied, newest first</p>
          </div>
          <ul class="divide-y divide-slate-50">
            @for (a of audit(); track a.id) {
              <li class="px-6 py-3.5 flex items-center gap-3 text-sm hover:bg-slate-50/50 transition-colors">
                <span
                  class="inline-block w-2 h-2 rounded-full shrink-0"
                  [class.bg-green-500]="a.status === 'Success'"
                  [class.bg-red-500]="a.status !== 'Success'"
                ></span>
                <span class="font-semibold text-slate-700 w-44 shrink-0">{{ a.eventType }}</span>
                <span class="text-slate-500 font-mono text-xs">{{ a.employeeId }}</span>
                <span class="text-slate-400 truncate flex-1">{{ a.message }}</span>
                <span class="text-xs text-slate-400 shrink-0">{{ a.atUtc | date:'shortTime' }}</span>
              </li>
            } @empty {
              <li class="px-6 py-10 text-center text-slate-400">No integration events yet.</li>
            }
          </ul>
        </section>

        <footer class="text-center text-xs text-slate-400 pb-4">
          WorkforceSync · mock Oracle HCM → ATOM feed → Channel&lt;T&gt; queue → idempotent processor → Angular
        </footer>
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
  readonly terminatedCount = () => this.employees().filter((e) => !e.isActive).length;
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
