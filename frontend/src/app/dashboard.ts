import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { WorkforceService } from './workforce.service';
import { AuditEntry, Employee, EmployeeChange, Health } from './models';

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
              Last ingest: {{ health()?.lastIngestAtUtc ? (health()!.lastIngestAtUtc | date:'HH:mm:ss') : '—' }}
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
              <p class="text-xs text-slate-400">auto-refreshes as the HCM feed publishes · changed rows flash green</p>
            </div>
            <span class="text-xs font-medium text-blue-600 bg-blue-50 border border-blue-100 rounded-full px-3 py-1">
              {{ employees().length }} records
            </span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-slate-500 border-b border-slate-100 bg-slate-50/50">
                  <th class="px-3 py-3"></th>
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
                  <tr
                    class="border-b border-slate-50 hover:bg-blue-50/30 transition-colors cursor-pointer"
                    [class.row-flash]="flashIds().has('e:' + e.employeeId)"
                    (click)="toggleExpand(e.employeeId)"
                  >
                    <td class="px-3 py-3.5 w-8">
                      <span
                        class="inline-block text-slate-400 transition-transform duration-200"
                        [class.rotate-90]="expandedId() === e.employeeId"
                      >▸</span>
                    </td>
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
                  @if (expandedId() === e.employeeId) {
                    <tr class="bg-slate-50/60">
                      <td colspan="7" class="px-6 py-4">
                        <div class="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-3">
                          Change history
                        </div>
                        @if (changesLoading()) {
                          <div class="text-sm text-slate-400">Loading…</div>
                        } @else if (changes().length === 0) {
                          <div class="text-sm text-slate-400">No recorded changes yet.</div>
                        } @else {
                          <ol class="space-y-3">
                            @for (c of changes(); track c.eventId) {
                              <li class="bg-white rounded-xl border border-slate-100 shadow-sm p-4">
                                <div class="flex items-center justify-between mb-2">
                                  <span
                                    class="inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold"
                                    [class.bg-blue-100]="c.eventType === 'Hire'"
                                    [class.bg-indigo-100]="c.eventType === 'PositionChange'"
                                    [class.bg-amber-100]="c.eventType === 'CompensationChange'"
                                    [class.bg-red-100]="c.eventType === 'Termination'"
                                    [class.text-blue-700]="c.eventType === 'Hire'"
                                    [class.text-indigo-700]="c.eventType === 'PositionChange'"
                                    [class.text-amber-700]="c.eventType === 'CompensationChange'"
                                    [class.text-red-700]="c.eventType === 'Termination'"
                                  >{{ c.eventType }}</span>
                                  <span class="text-xs text-slate-400 tabular-nums">{{ c.atUtc | date:'HH:mm:ss' }}</span>
                                </div>
                                <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1.5">
                                  @for (f of c.fields; track f.field) {
                                    <div class="flex items-baseline gap-2 text-sm">
                                      <dt class="w-28 shrink-0 text-slate-500">{{ f.field }}</dt>
                                      <dd class="flex items-center gap-1.5 flex-wrap">
                                        @if (f.oldValue) {
                                          <span class="text-slate-400 line-through decoration-slate-300">{{ f.oldValue }}</span>
                                          <span class="text-slate-300">→</span>
                                        }
                                        <span class="font-medium text-slate-800">{{ f.newValue }}</span>
                                      </dd>
                                    </div>
                                  }
                                </dl>
                              </li>
                            }
                          </ol>
                        }
                      </td>
                    </tr>
                  }
                } @empty {
                  <tr><td colspan="7" class="px-6 py-10 text-center text-slate-400">No employees yet — waiting for the first hire event.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <!-- Audit log -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 bg-gradient-to-r from-white to-slate-50/50">
            <h2 class="font-semibold text-slate-800">Integration audit log</h2>
            <p class="text-xs text-slate-400">every event the pipeline applied, newest first · new rows flash green</p>
          </div>
          <ul class="divide-y divide-slate-50">
            @for (a of audit(); track a.id) {
              <li
                class="px-6 py-3.5 flex items-center gap-3 text-sm hover:bg-slate-50/50 transition-colors"
                [class.row-flash]="flashIds().has('a:' + a.id)"
              >
                <span
                  class="inline-block w-2 h-2 rounded-full shrink-0"
                  [class.bg-green-500]="a.status === 'Success'"
                  [class.bg-red-500]="a.status !== 'Success'"
                ></span>
                <span class="font-semibold text-slate-700 w-44 shrink-0">{{ a.eventType }}</span>
                <span class="text-slate-500 font-mono text-xs">{{ a.employeeId }}</span>
                <span class="text-slate-400 truncate flex-1">{{ a.message }}</span>
                <span class="text-xs text-slate-400 shrink-0 tabular-nums">{{ a.atUtc | date:'HH:mm:ss' }}</span>
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

  // Expanded employee row + its change history.
  readonly expandedId = signal<string | null>(null);
  readonly changes = signal<EmployeeChange[]>([]);
  readonly changesLoading = signal(false);

  /**
   * Rows that should flash green. Keyed by a stable id with a source prefix
   * ('a:<auditId>' for audit rows, 'e:<employeeId>' for employee rows). A row
   * is added when it is new or its data changed, then removed after the
   * animation finishes so a later change can re-trigger it.
   */
  readonly flashIds = signal<Set<string>>(new Set());

  readonly activeCount = () => this.employees().filter((e) => e.isActive).length;
  readonly terminatedCount = () => this.employees().filter((e) => !e.isActive).length;
  readonly departmentCount = () =>
    new Set(this.employees().map((e) => e.department)).size;

  private timer: ReturnType<typeof setInterval> | null = null;

  // Previous-state tracking (used to detect new/changed rows between refreshes).
  private prevAuditIds = new Set<number>();
  private prevEmployeeUpdated = new Map<string, string>();
  private auditLoaded = false;
  private employeesLoaded = false;

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
      next: (res) => {
        if (this.employeesLoaded) {
          for (const e of res.items) {
            // New hire (no prior record) or any field change (updatedAt moved).
            if (this.prevEmployeeUpdated.get(e.employeeId) !== e.updatedAtUtc) {
              this.flash('e:' + e.employeeId);
            }
          }
        }
        this.prevEmployeeUpdated = new Map(res.items.map((e) => [e.employeeId, e.updatedAtUtc]));
        this.employeesLoaded = true;
        this.employees.set(res.items);
      },
      error: () => {
        /* interceptor handles 401 → logout; ignore transient errors */
      },
    });
    this.workforce.audit(30).subscribe({
      next: (items) => {
        if (this.auditLoaded) {
          for (const a of items) {
            if (!this.prevAuditIds.has(a.id)) {
              this.flash('a:' + a.id);
            }
          }
        }
        this.prevAuditIds = new Set(items.map((a) => a.id));
        this.auditLoaded = true;
        this.audit.set(items);
      },
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

  /** Toggle the expanded change-history row for an employee. */
  toggleExpand(employeeId: string): void {
    if (this.expandedId() === employeeId) {
      this.expandedId.set(null);
      this.changes.set([]);
      return;
    }
    this.expandedId.set(employeeId);
    this.changes.set([]);
    this.changesLoading.set(true);
    this.workforce.employeeChanges(employeeId).subscribe({
      next: (items) => {
        this.changes.set(items);
        this.changesLoading.set(false);
      },
      error: () => {
        this.changes.set([]);
        this.changesLoading.set(false);
      },
    });
  }

  /** Mark a row to flash green, then clear it once the animation has run. */
  private flash(key: string): void {
    const next = new Set(this.flashIds());
    next.add(key);
    this.flashIds.set(next);

    // Remove after the 1.2s animation so a future change can re-trigger it.
    setTimeout(() => {
      const current = this.flashIds();
      if (current.has(key)) {
        const cleaned = new Set(current);
        cleaned.delete(key);
        this.flashIds.set(cleaned);
      }
    }, 1300);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
