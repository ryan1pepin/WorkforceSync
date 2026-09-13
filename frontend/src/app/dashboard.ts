import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { WorkforceService } from './workforce.service';
import { AuditEntry, DeadLetter, Employee, EmployeeChange, Health, Metrics, Position } from './models';

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

      <!-- Tab navigation -->
      <nav class="max-w-7xl mx-auto px-6 pt-6">
        <div class="inline-flex items-center gap-1 bg-white rounded-xl shadow-md shadow-slate-200/50 border border-slate-100 p-1">
          <button
            (click)="setTab('overview')"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200"
            [class.bg-blue-600]="tab() === 'overview'"
            [class.text-white]="tab() === 'overview'"
            [class.shadow-md]="tab() === 'overview'"
            [class.text-slate-600]="tab() !== 'overview'"
            [class.hover:bg-slate-100]="tab() !== 'overview'"
          >Overview</button>
          <button
            (click)="setTab('workforce')"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200"
            [class.bg-blue-600]="tab() === 'workforce'"
            [class.text-white]="tab() === 'workforce'"
            [class.shadow-md]="tab() === 'workforce'"
            [class.text-slate-600]="tab() !== 'workforce'"
            [class.hover:bg-slate-100]="tab() !== 'workforce'"
          >Workforce</button>
          <button
            (click)="setTab('positions')"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200"
            [class.bg-blue-600]="tab() === 'positions'"
            [class.text-white]="tab() === 'positions'"
            [class.shadow-md]="tab() === 'positions'"
            [class.text-slate-600]="tab() !== 'positions'"
            [class.hover:bg-slate-100]="tab() !== 'positions'"
          >Positions</button>
          <button
            (click)="setTab('audit')"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200"
            [class.bg-blue-600]="tab() === 'audit'"
            [class.text-white]="tab() === 'audit'"
            [class.shadow-md]="tab() === 'audit'"
            [class.text-slate-600]="tab() !== 'audit'"
            [class.hover:bg-slate-100]="tab() !== 'audit'"
          >Audit Log</button>
          <button
            (click)="setTab('deadletter')"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200"
            [class.bg-blue-600]="tab() === 'deadletter'"
            [class.text-white]="tab() === 'deadletter'"
            [class.shadow-md]="tab() === 'deadletter'"
            [class.text-slate-600]="tab() !== 'deadletter'"
            [class.hover:bg-slate-100]="tab() !== 'deadletter'"
          >Dead Letter</button>
        </div>
      </nav>

      <main class="max-w-7xl mx-auto px-6 py-8 space-y-6">
        @if (tab() === 'overview') {
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
        }

        @if (tab() === 'workforce') {
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
                  <th class="px-4 py-3 font-medium">Person #</th>
                  <th class="px-4 py-3 font-medium">Name</th>
                  <th class="px-4 py-3 font-medium">Job title</th>
                  <th class="px-4 py-3 font-medium">Job</th>
                  <th class="px-4 py-3 font-medium">Grade</th>
                  <th class="px-4 py-3 font-medium">Department</th>
                  <th class="px-4 py-3 font-medium">Location</th>
                  <th class="px-4 py-3 font-medium">Supervisor</th>
                  <th class="px-4 py-3 font-medium">Start date</th>
                  <th class="px-4 py-3 font-medium text-right">Base salary</th>
                  <th class="px-4 py-3 font-medium">Status</th>
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
                    <td class="px-4 py-3.5 font-mono text-xs text-slate-500">{{ e.personNumber }}</td>
                    <td class="px-4 py-3.5">
                      <div class="font-medium text-slate-800">{{ e.firstName }} {{ e.lastName }}</div>
                      <div class="text-xs text-slate-400">{{ e.email }}</div>
                    </td>
                    <td class="px-4 py-3.5 text-slate-600">{{ e.jobTitle }}</td>
                    <td class="px-4 py-3.5 text-slate-500">{{ e.job }}</td>
                    <td class="px-4 py-3.5">
                      <span class="inline-block rounded-md bg-indigo-50 text-indigo-600 px-2 py-0.5 text-xs font-medium">{{ e.grade }}</span>
                    </td>
                    <td class="px-4 py-3.5">
                      <span class="inline-block rounded-md bg-slate-100 text-slate-600 px-2 py-0.5 text-xs font-medium">{{ e.department }}</span>
                    </td>
                    <td class="px-4 py-3.5 text-slate-500">{{ e.workLocation ?? '—' }}</td>
                    <td class="px-4 py-3.5 text-slate-500">{{ e.supervisor ?? '—' }}</td>
                    <td class="px-4 py-3.5 text-slate-600">{{ e.startDate | date:'mediumDate' }}</td>
                    <td class="px-4 py-3.5 text-right text-slate-800 font-semibold">
                      {{ e.baseSalary | number }} <span class="text-xs text-slate-400 font-normal">{{ e.currency }}</span>
                    </td>
                    <td class="px-4 py-3.5">
                      @if (e.isActive) {
                        <span class="inline-block rounded-full bg-green-100 text-green-700 px-2.5 py-0.5 text-xs font-semibold">Active</span>
                      } @else {
                        <span class="inline-block rounded-full bg-red-100 text-red-700 px-2.5 py-0.5 text-xs font-semibold">Terminated</span>
                      }
                    </td>
                  </tr>
                  @if (expandedId() === e.employeeId) {
                    <tr class="bg-slate-50/60">
                      <td colspan="12" class="px-6 py-4">
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
                                    [class.bg-indigo-100]="c.eventType === 'Transfer'"
                                    [class.bg-amber-100]="c.eventType === 'PayChange'"
                                    [class.bg-violet-100]="c.eventType === 'Promotion'"
                                    [class.bg-teal-100]="c.eventType === 'Rehire'"
                                    [class.bg-red-100]="c.eventType === 'Termination'"
                                    [class.text-blue-700]="c.eventType === 'Hire'"
                                    [class.text-indigo-700]="c.eventType === 'Transfer'"
                                    [class.text-amber-700]="c.eventType === 'PayChange'"
                                    [class.text-violet-700]="c.eventType === 'Promotion'"
                                    [class.text-teal-700]="c.eventType === 'Rehire'"
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
                  <tr><td colspan="12" class="px-6 py-10 text-center text-slate-400">No employees yet — waiting for the first hire event.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        }

        @if (tab() === 'positions') {
        <!-- Positions -->
        <section class="bg-white rounded-2xl shadow-lg shadow-slate-200/60 border border-slate-100 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex flex-col gap-3 bg-gradient-to-r from-white to-slate-50/50">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="font-semibold text-slate-800">Positions</h2>
                <p class="text-xs text-slate-400">the job slots the HCM feed assigns people to — job, grade, department, location</p>
              </div>
              <span class="text-xs font-medium text-blue-600 bg-blue-50 border border-blue-100 rounded-full px-3 py-1">
                {{ positions().length }} positions
              </span>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <input
                type="search"
                placeholder="Search position, job, department…"
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500/40 w-56"
                [value]="posSearch()"
                (input)="onPosSearch($any($event).target.value)"
              />
              <select
                class="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
                [value]="posDepartment()"
                (change)="onPosDepartment($any($event).target.value)"
              >
                <option value="">All departments</option>
                @for (d of positionDepartments(); track d) {
                  <option [value]="d">{{ d }}</option>
                }
              </select>
              @if (hasPosFilters()) {
                <button
                  (click)="clearPosFilters()"
                  class="text-xs font-medium text-slate-500 hover:text-slate-700 underline underline-offset-2"
                >Clear</button>
              }
            </div>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-slate-500 border-b border-slate-100 bg-slate-50/50">
                  <th class="px-4 py-3 font-medium">Position</th>
                  <th class="px-4 py-3 font-medium">Job</th>
                  <th class="px-4 py-3 font-medium">Grade</th>
                  <th class="px-4 py-3 font-medium">Department</th>
                  <th class="px-4 py-3 font-medium">Location</th>
                  <th class="px-4 py-3 font-medium">Supervisor</th>
                  <th class="px-4 py-3 font-medium">Created</th>
                </tr>
              </thead>
              <tbody>
                @for (p of filteredPositions(); track p.positionId) {
                  <tr class="border-b border-slate-50 hover:bg-blue-50/30 transition-colors">
                    <td class="px-4 py-3.5">
                      <div class="font-medium text-slate-800">{{ p.jobTitle }}</div>
                      <div class="text-xs text-slate-400 font-mono">{{ p.positionId }}</div>
                    </td>
                    <td class="px-4 py-3.5 text-slate-600">{{ p.job }}</td>
                    <td class="px-4 py-3.5">
                      <span class="inline-block rounded-md bg-indigo-50 text-indigo-600 px-2 py-0.5 text-xs font-medium">{{ p.grade }}</span>
                    </td>
                    <td class="px-4 py-3.5">
                      <span class="inline-block rounded-md bg-slate-100 text-slate-600 px-2 py-0.5 text-xs font-medium">{{ p.department }}</span>
                    </td>
                    <td class="px-4 py-3.5 text-slate-500">{{ p.location ?? '—' }}</td>
                    <td class="px-4 py-3.5 text-slate-500">{{ p.supervisor ?? '—' }}</td>
                    <td class="px-4 py-3.5 text-slate-500">{{ p.createdAtUtc | date:'mediumDate' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="px-6 py-10 text-center text-slate-400">No positions yet — they appear as the feed assigns people to roles.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        }

        @if (tab() === 'audit') {
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
                <option value="Transfer">Transfer</option>
                <option value="PayChange">Pay change</option>
                <option value="Promotion">Promotion</option>
                <option value="Rehire">Rehire</option>
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
        }

        @if (tab() === 'deadletter') {
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
        }

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
  readonly positions = signal<Position[]>([]);

  // Active tab.
  readonly tab = signal<'overview' | 'workforce' | 'positions' | 'audit' | 'deadletter'>('overview');
  setTab(t: 'overview' | 'workforce' | 'positions' | 'audit' | 'deadletter'): void { this.tab.set(t); }

  // Expanded employee row + its change history.
  readonly expandedId = signal<string | null>(null);
  readonly changes = signal<EmployeeChange[]>([]);
  readonly changesLoading = signal(false);

  // Employee table filters.
  readonly empSearch = signal('');
  readonly empDepartment = signal('');
  readonly empStatus = signal(''); // '' | 'active' | 'terminated'

  // Positions table filters.
  readonly posSearch = signal('');
  readonly posDepartment = signal('');

  // Audit table filters.
  readonly auditStatus = signal(''); // '' | 'Success' | 'Rejected' | 'Error'
  readonly auditType = signal('');   // '' | 'Hire' | 'Transfer' | 'PayChange' | 'Promotion' | 'Rehire' | 'Termination'
  readonly auditSearch = signal('');

  /** Distinct departments currently in the employee list (for the filter dropdown). */
  readonly departments = () =>
    [...new Set(this.employees().map((e) => e.department))].sort();

  /** Distinct departments currently in the position list (for the filter dropdown). */
  readonly positionDepartments = () =>
    [...new Set(this.positions().map((p) => p.department))].sort();

  /** Positions filtered by the search box + department dropdown. */
  readonly filteredPositions = () => {
    const q = this.posSearch().trim().toLowerCase();
    const dept = this.posDepartment();
    return this.positions().filter((p) => {
      if (dept && p.department !== dept) return false;
      if (!q) return true;
      return (
        p.jobTitle.toLowerCase().includes(q) ||
        p.job.toLowerCase().includes(q) ||
        p.department.toLowerCase().includes(q) ||
        p.positionId.toLowerCase().includes(q)
      );
    });
  };

  readonly hasEmpFilters = () =>
    this.empSearch() !== '' || this.empDepartment() !== '' || this.empStatus() !== '';
  readonly hasPosFilters = () =>
    this.posSearch() !== '' || this.posDepartment() !== '';
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
    this.workforce.positions().subscribe({
      next: (items) => this.positions.set(items),
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

  // ── Positions table filters ───────────────────────────────────────────────
  onPosSearch(v: string): void { this.posSearch.set(v); }
  onPosDepartment(v: string): void { this.posDepartment.set(v); }
  clearPosFilters(): void {
    this.posSearch.set(''); this.posDepartment.set('');
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
