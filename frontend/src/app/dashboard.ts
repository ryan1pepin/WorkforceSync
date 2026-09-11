import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { WorkforceService } from './workforce.service';
import { AuditEntry, DeadLetter, Employee, EmployeeChange, Health, Metrics } from './models';

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

        <!-- Pipeline metrics strip -->
        @if (metrics(); ) {
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <div class="bg-white rounded-xl shadow-md shadow-slate-200/50 border border-slate-100 px-4 py-3 hover:shadow-lg transition-all duration-200">
              <div class="text-xs font-medium text-slate-500">Events applied</div>
              <div class="mt-1 text-xl font-bold text-green-600">{{ metrics()!.applied }}</div>
            </div>
            <div class="bg-white rounded-xl shadow-md shadow-slate-200/50 border border-slate-100 px-4 py-3 hover:shadow-lg transition-all duration-200">
              <div class="text-xs font-medium text-slate-500">Rejected</div>
              <div class="mt-1 text-xl font-bold text-amber-600">{{ metrics()!.rejected }}</div>
            </div>
            <div class="bg-white rounded-xl shadow-md shadow-slate-200/50 border border-slate-100 px-4 py-3 hover:shadow-lg transition-all duration-200">
              <div class="text-xs font-medium text-slate-500">Dead-letter pending</div>
              <div class="mt-1 text-xl font-bold text-red-600">{{ metrics()!.deadLetterPending }}</div>
            </div>
            <div class="bg-white rounded-xl shadow-md shadow-slate-200/50 border border-slate-100 px-4 py-3 hover:shadow-lg transition-all duration-200">
              <div class="text-xs font-medium text-slate-500">Events / min</div>
              <div class="mt-1 text-xl font-bold text-blue-600">{{ metrics()!.eventsLastMinute }}</div>
            </div>
          </div>
        }

        <!-- Employees -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex flex-col gap-3 bg-gradient-to-r from-white to-slate-50/50">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="font-semibold text-slate-800">Employees</h2>
                <p class="text-xs text-slate-400">auto-refreshes as the HCM feed publishes · click a row for change history</p>
              </div>
              <span class="text-xs font-medium text-blue-600 bg-blue-50 border border-blue-100 rounded-full px-3 py-1">
                {{ employees().length }} records
              </span>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <input
                type="search"
                placeholder="Search name, email, title…"
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500/40 w-56"
                [value]="empSearch()"
                (input)="onEmpSearch($any($event).target.value)"
              />
              <select
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
                [value]="empDepartment()"
                (change)="onEmpDepartment($any($event).target.value)"
              >
                <option value="">All departments</option>
                @for (d of departments(); track d) {
                  <option [value]="d">{{ d }}</option>
                }
              </select>
              <select
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
                [value]="empStatus()"
                (change)="onEmpStatus($any($event).target.value)"
              >
                <option value="">All statuses</option>
                <option value="active">Active</option>
                <option value="terminated">Terminated</option>
              </select>
              @if (hasEmpFilters()) {
                <button
                  (click)="clearEmpFilters()"
                  class="text-xs font-medium text-slate-500 hover:text-slate-700 underline underline-offset-2"
                >Clear</button>
              }
            </div>
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
          <div class="px-6 py-4 border-b border-slate-100 flex flex-col gap-3 bg-gradient-to-r from-white to-slate-50/50">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="font-semibold text-slate-800">Integration audit log</h2>
                <p class="text-xs text-slate-400">every event the pipeline applied or rejected, newest first</p>
              </div>
              <span class="text-xs font-medium text-blue-600 bg-blue-50 border border-blue-100 rounded-full px-3 py-1">
                {{ audit().length }} events
              </span>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <select
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
                [value]="auditStatus()"
                (change)="onAuditStatus($any($event).target.value)"
              >
                <option value="">All statuses</option>
                <option value="Success">Success</option>
                <option value="Rejected">Rejected</option>
                <option value="Error">Error</option>
              </select>
              <select
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
                [value]="auditType()"
                (change)="onAuditType($any($event).target.value)"
              >
                <option value="">All event types</option>
                <option value="Hire">Hire</option>
                <option value="PositionChange">Position change</option>
                <option value="CompensationChange">Comp change</option>
                <option value="Termination">Termination</option>
              </select>
              <input
                type="search"
                placeholder="Search employee or detail…"
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500/40 w-56"
                [value]="auditSearch()"
                (input)="onAuditSearch($any($event).target.value)"
              />
              @if (hasAuditFilters()) {
                <button
                  (click)="clearAuditFilters()"
                  class="text-xs font-medium text-slate-500 hover:text-slate-700 underline underline-offset-2"
                >Clear</button>
              }
            </div>
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
                  [class.bg-amber-500]="a.status === 'Rejected'"
                  [class.bg-red-500]="a.status === 'Error'"
                ></span>
                <span
                  class="inline-block rounded-full px-2 py-0.5 text-xs font-semibold shrink-0 w-24 text-center"
                  [class.bg-green-100]="a.status === 'Success'"
                  [class.bg-amber-100]="a.status === 'Rejected'"
                  [class.bg-red-100]="a.status === 'Error'"
                  [class.text-green-700]="a.status === 'Success'"
                  [class.text-amber-700]="a.status === 'Rejected'"
                  [class.text-red-700]="a.status === 'Error'"
                >{{ a.status }}</span>
                <span class="font-semibold text-slate-700 w-40 shrink-0">{{ a.eventType }}</span>
                <span class="text-slate-500 font-mono text-xs shrink-0">{{ a.employeeId }}</span>
                <span class="text-slate-500 truncate flex-1">{{ a.message }}</span>
                <span class="text-xs text-slate-400 shrink-0 tabular-nums">{{ a.atUtc | date:'HH:mm:ss' }}</span>
              </li>
            } @empty {
              <li class="px-6 py-10 text-center text-slate-400">No integration events yet.</li>
            }
          </ul>
        </section>

        <!-- Dead-letter queue -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex flex-col gap-3 bg-gradient-to-r from-white to-slate-50/50">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="font-semibold text-slate-800">Dead-letter queue</h2>
                <p class="text-xs text-slate-400">events the pipeline couldn't apply — inspect, replay, or discard</p>
              </div>
              <span class="text-xs font-medium text-red-600 bg-red-50 border border-red-100 rounded-full px-3 py-1">
                {{ pendingDeadLetterCount() }} pending
              </span>
            </div>
          </div>
          <ul class="divide-y divide-slate-50">
            @for (d of deadLetters(); track d.id) {
              <li class="px-6 py-3.5 flex items-start gap-3 text-sm hover:bg-slate-50/50 transition-colors">
                <span
                  class="inline-block rounded-full px-2 py-0.5 text-xs font-semibold shrink-0 mt-0.5"
                  [class.bg-amber-100]="d.status === 'Pending'"
                  [class.bg-green-100]="d.status === 'Replayed'"
                  [class.bg-slate-100]="d.status === 'Discarded'"
                  [class.text-amber-700]="d.status === 'Pending'"
                  [class.text-green-700]="d.status === 'Replayed'"
                  [class.text-slate-500]="d.status === 'Discarded'"
                >{{ d.status }}</span>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="font-semibold text-slate-700">{{ d.eventType }}</span>
                    <span class="text-slate-400 font-mono text-xs">{{ d.employeeId }}</span>
                    <span class="text-xs text-slate-400 tabular-nums">{{ d.createdAtUtc | date:'HH:mm:ss' }}</span>
                  </div>
                  <p class="text-xs text-slate-500 mt-1 truncate" [title]="d.reason">{{ d.reason }}</p>
                  @if (d.lastResult) {
                    <p class="text-xs text-slate-400 mt-0.5">last replay: {{ d.lastResult }}</p>
                  }
                </div>
                @if (d.status === 'Pending') {
                  <div class="flex items-center gap-2 shrink-0">
                    <button
                      (click)="replay(d)"
                      class="rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium px-3 py-1.5 transition shadow-sm"
                    >
                      Replay
                    </button>
                    <button
                      (click)="discard(d)"
                      class="rounded-lg bg-white border border-slate-200 hover:bg-slate-50 text-slate-600 text-xs font-medium px-3 py-1.5 transition"
                    >
                      Discard
                    </button>
                  </div>
                }
              </li>
            } @empty {
              <li class="px-6 py-10 text-center text-slate-400">
                No dead-lettered events — the pipeline is clean.
              </li>
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
  readonly metrics = signal<Metrics | null>(null);
  readonly deadLetters = signal<DeadLetter[]>([]);

  // Expanded employee row + its change history.
  readonly expandedId = signal<string | null>(null);
  readonly changes = signal<EmployeeChange[]>([]);
  readonly changesLoading = signal(false);

  // Employee table filters.
  readonly empSearch = signal('');
  readonly empDepartment = signal('');
  readonly empStatus = signal(''); // '' | 'active' | 'terminated'

  // Audit table filters.
  readonly auditStatus = signal(''); // '' | 'Success' | 'Rejected' | 'Error'
  readonly auditType = signal('');   // '' | 'Hire' | 'PositionChange' | 'CompensationChange' | 'Termination'
  readonly auditSearch = signal('');

  /** Distinct departments currently in the employee list (for the filter dropdown). */
  readonly departments = () =>
    [...new Set(this.employees().map((e) => e.department))].sort();

  readonly hasEmpFilters = () =>
    this.empSearch() !== '' || this.empDepartment() !== '' || this.empStatus() !== '';
  readonly hasAuditFilters = () =>
    this.auditStatus() !== '' || this.auditType() !== '' || this.auditSearch() !== '';

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
  readonly pendingDeadLetterCount = () =>
    this.deadLetters().filter((d) => d.status === 'Pending').length;

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
    this.workforce.employees(1, 50, {
      status: this.empStatus() || undefined,
      department: this.empDepartment() || undefined,
      search: this.empSearch() || undefined,
    }).subscribe({
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
    this.workforce.audit(30, this.auditStatus() || undefined, this.auditType() || undefined, this.auditSearch() || undefined).subscribe({
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
    this.workforce.metrics().subscribe({
      next: (m) => this.metrics.set(m),
      error: () => {
        /* ignore */
      },
    });
    this.workforce.deadLetter().subscribe({
      next: (items) => this.deadLetters.set(items),
      error: () => {
        /* ignore */
      },
    });
  }

  // ── Employee table filters ────────────────────────────────────────────────
  onEmpSearch(v: string): void { this.empSearch.set(v); this.refresh(); }
  onEmpDepartment(v: string): void { this.empDepartment.set(v); this.refresh(); }
  onEmpStatus(v: string): void { this.empStatus.set(v); this.refresh(); }
  clearEmpFilters(): void {
    this.empSearch.set(''); this.empDepartment.set(''); this.empStatus.set('');
    this.refresh();
  }

  // ── Audit table filters ───────────────────────────────────────────────────
  onAuditStatus(v: string): void { this.auditStatus.set(v); this.refresh(); }
  onAuditType(v: string): void { this.auditType.set(v); this.refresh(); }
  onAuditSearch(v: string): void { this.auditSearch.set(v); this.refresh(); }
  clearAuditFilters(): void {
    this.auditStatus.set(''); this.auditType.set(''); this.auditSearch.set('');
    this.refresh();
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

  // ── Dead-letter queue ─────────────────────────────────────────────────────
  replay(d: DeadLetter): void {
    this.workforce.replayDeadLetter(d.id).subscribe({
      next: () => this.refresh(),
      error: () => this.refresh(),
    });
  }

  discard(d: DeadLetter): void {
    this.workforce.discardDeadLetter(d.id).subscribe({
      next: () => this.refresh(),
      error: () => this.refresh(),
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
