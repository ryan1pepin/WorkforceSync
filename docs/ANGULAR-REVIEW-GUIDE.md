# WorkforceSync — Angular Review Guide

> Interview: Southern Company, Wed/Thu 9/16–17. This guide gets you fluent in every
> Angular decision in `frontend/` so you can explain *why* each choice was made,
> defend it, and know its tradeoffs. C# side is your strength — this is the gap.
>
> **How to use it:** read top-to-bottom once (~45 min), then do the self-test at the
> end out loud. Fix list at the bottom — do #1 and #2 before the interview.

---

## 0. The 60-second Angular story (say this first if asked about the frontend)

> "The dashboard is Angular 20 — standalone components, signals for UI state,
> functional router guards and HTTP interceptors, template-driven forms, Tailwind v4.
> Auth is a JWT pair: a short-lived access token attached by an interceptor, and when
> it 401s the interceptor transparently refreshes and replays. The dashboard polls
> the API every 5 seconds, diffs the results, and flashes the rows that changed —
> so you watch the pipeline land live without any manual refresh."

---

## 1. Read the code in this order

- [ ] `src/main.ts` → `src/app/app.config.ts` → `src/app/app.ts` (bootstrap)
- [ ] `src/app/app.routes.ts` (routing + guard)
- [ ] `src/app/auth.service.ts` → `src/app/auth.interceptor.ts` (auth — the core)
- [ ] `src/app/models.ts` → `src/app/workforce.service.ts` (data layer)
- [ ] `src/app/login.ts` (simple component, forms)
- [ ] `src/app/dashboard.ts` (the big one — 492 lines)
- [ ] `angular.json`, `proxy.conf.json`, `tsconfig.json`, `src/styles.css` (build/config)

---

## 2. Bootstrap & app config

### `main.ts` — standalone bootstrap
```ts
bootstrapApplication(App, appConfig).catch((err) => console.error(err));
```
- No `AppModule`. `bootstrapApplication` is the standalone entry point (Angular 15+).
- `appConfig` is an `ApplicationConfig` object — a bag of providers, not a module.

### `app.config.ts` — the four providers, know each one
```ts
providers: [
  provideBrowserGlobalErrorListeners(),
  provideZoneChangeDetection({ eventCoalescing: true }),
  provideHttpClient(withInterceptors([authInterceptor])),
  provideRouter(routes, withHashLocation()),
]
```
| Provider | What it does | Interview line |
|---|---|---|
| `provideBrowserGlobalErrorListeners()` | Angular 20's global error handling (unhandled promise rejections, etc.) | "Newer, declarative replacement for wiring `ErrorHandler` by hand." |
| `provideZoneChangeDetection({ eventCoalescing: true })` | Enables zone.js change detection; coalescing collapses multiple CD cycles per tick into one | "Zone.js patches async APIs (setTimeout, XHR, click) and tells Angular when to check for changes. Coalescing avoids redundant CD passes." |
| `provideHttpClient(withInterceptors([authInterceptor]))` | Registers the functional auth interceptor on every `HttpClient` | "Central place to attach the Bearer token and handle 401s — no component knows about tokens." |
| `provideRouter(routes, withHashLocation())` | Router with hash-based URLs (`/#/dashboard`) | "Hash location means any static host serves it with zero SPA-fallback config. Tradeoff: uglier URLs — fine for a demo, I'd use path-based routing behind a real web server in production." |

### `app.ts` — root component
Just `<router-outlet />`. `standalone: true` is the default in v20 (the flag is
redundant but explicit). Nothing else lives here — no shared header, deliberately.

### zone.js — be ready for "why not zoneless?"
- zone.js = battle-tested, works with any async source, zero code changes.
- Zoneless (signals-driven CD) is available in v20 but still maturing; you opt in
  per-app and give up zone's automatic CD triggers.
- **Answer:** "For a demo that has to be rock-solid, zone.js is the safe choice.
  I've read the zoneless requirements — it's the direction Angular is heading."

---

## 3. Routing & the auth guard — `app.routes.ts`

```ts
const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.parseUrl('/login');
};

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' },
];
```
Key points:
- **Functional guard** (`CanActivateFn`): a plain function, no class. `inject()`
  works inside it because the router calls it in an injection context.
- Returns `true` or a **`UrlTree`** (`router.parseUrl('/login')`) — returning a
  UrlTree is the idiomatic redirect (vs. `router.navigate` as a side effect).
- `pathMatch: 'full'` on `''` so the empty path only matches exactly.
- Wildcard `**` → dashboard → guard bounces unauthenticated users to login.
  So there is exactly one redirect path to login; no component ever "decides" auth.

---

## 4. Auth — the heart of the Angular review

### 4.1 `auth.service.ts` — signals + localStorage

```ts
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  readonly user = signal<User | null>(this.readUser());
  readonly isAuthenticated = signal<boolean>(!!this.readUser());
  ...
  get accessToken(): string | null { return localStorage.getItem(ACCESS_KEY); }
  get refreshToken(): string | null { return localStorage.getItem(REFRESH_KEY); }
```
- `providedIn: 'root'` = tree-shakable singleton (one instance app-wide, no module import).
- **Signals for reactive UI state** (`user`, `isAuthenticated`) — templates that read
  them re-render automatically when `.set()` is called. No subscriptions to manage.
- **Tokens live in localStorage** (keys `wfs.*`), read via getters — so a page
  refresh restores the session, and the interceptor always reads the *current* token.
- `login` / `register` / `refresh` all end in `storeSession(res)`: persist the new
  pair + user, flip the two signals. One place where session state changes.
- `logout()`: best-effort `POST /auth/logout` (server revokes the token family),
  then `clearSession()` locally regardless of the response.
- **Why the backend matters here:** access token = 5 min, refresh = 30 days,
  **rotated on every use with family-wide reuse detection** (a replayed old refresh
  token revokes the whole family). The frontend contract: *always persist the new
  refresh token from the response* — which `storeSession` does.

### 4.2 `auth.interceptor.ts` — functional interceptor, 401 → refresh → replay

```ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const http = inject(HttpClient);

  const token = auth.accessToken;
  const authorized = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorized).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || !auth.refreshToken) {
        return throwError(() => err);
      }
      return auth.refresh().pipe(
        switchMap(() => next(authorized)),          // ⚠️ see bug below
        catchError((refreshErr) => {
          auth.logout();
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
```
Walk it in this order when asked:
1. `HttpInterceptorFn` is a plain function `(req, next) => Observable` — registered
   once in `app.config.ts`, applies to every `HttpClient` call.
2. Clone the request with the Bearer header (never mutate the original).
3. On **401 only** (and only if a refresh token exists): call `auth.refresh()`,
   then replay the original request.
4. If the refresh itself fails: `auth.logout()` (clears session) and rethrow —
   the guard bounces the user to login on next navigation.
5. Non-401 errors pass through untouched — the interceptor only owns auth failures.

### ✅ 4.3 FIXED — replayed request now carries the FRESH token

**Original bug:** `authorized` was captured **before** the refresh, so
`switchMap(() => next(authorized))` replayed the request with the **old, expired**
Authorization header. The replay 401'd again; the error propagated to the caller.
The demo never showed it: the dashboard polls every 5s and swallows errors, so the
*next* poll (fresh token) succeeded.

**Fix (applied):** re-read the token *after* rotation, clone the original request
with it:
```ts
return auth.refresh().pipe(
  switchMap(() => {
    const fresh = auth.accessToken; // read AFTER rotation
    const replay = fresh
      ? req.clone({ setHeaders: { Authorization: `Bearer ${fresh}` } })
      : req;
    return next(replay);
  }),
  catchError((refreshErr: HttpErrorResponse) => {
    auth.logout();
    return throwError(() => refreshErr);
  })
);
```
**Interview line:** "The first version replayed with the pre-refresh token — I
caught it in review and fixed it to re-read the rotated token before replaying."
A bug you found and fixed is a better story than one that was never there.

### ✅ 4.4 FIXED — concurrent-refresh race (in-flight dedup)

Three parallel requests (the dashboard fires 3 per poll) can all 401 at once.
Each calling `auth.refresh()` independently would mean:
- refresh #1: RT1 → RT2 ✓
- refresh #2 (still holding RT1): RT1 is now revoked → **reuse detected →
  whole family revoked → logged out.**

**Fix (applied):** `AuthService.refresh()` now dedupes in-flight calls — the
first caller creates the refresh Observable, concurrent callers share it, and
`finalize()` clears it on completion:
```ts
private refreshing: Observable<User> | null = null;

refresh(): Observable<User> {
  const rt = this.refreshToken;
  if (!rt) return throwError(() => new Error('No refresh token'));
  if (!this.refreshing) {
    this.refreshing = this.http
      .post<AuthResponse>('/auth/refresh', { refreshToken: rt })
      .pipe(
        map((res) => this.storeSession(res)),
        finalize(() => (this.refreshing = null)),
      );
  }
  return this.refreshing;
}
```
**Interview line:** "N parallel 401s trigger exactly one rotation — the others
share the in-flight Observable. Without that, my own backend's reuse detection
would have logged the user out."

---

## 5. Data layer

### `models.ts`
Plain TS interfaces mirroring the API DTOs (camelCase — the API sets
`PropertyNamingPolicy = CamelCase`). Note `PagedResult<T>` — generic page envelope
(`items`, `totalCount`, `page`, `pageSize`). Know that `EmployeeChange`/
`EmployeeChangeField` back the expandable change-history rows.

### `workforce.service.ts`
- `@Injectable({ providedIn: 'root' })`, `private http = inject(HttpClient)`.
- Every method returns an `Observable` — the component decides when to subscribe.
- Query params built conditionally (`if (opts.status) params['status'] = ...`) —
  **server-side filtering**: the API does the filtering, the UI just passes params.
  This is a deliberate decision — don't say "the dashboard filters the list."
- Endpoints: `employees(page, pageSize, {status, department, search})`,
  `employeeChanges(id)`, `positions()`, `audit(limit, status, eventType, search)`,
  `health()`.

---

## 6. Components

### 6.1 `login.ts` — the simple one
- **Template-driven form**: `[(ngModel)]` two-way binding to plain class fields
  (`email`, `password`, `firstName`, `lastName`) + `name` attributes + `ngSubmit`.
  `FormsModule` in `imports`.
- **Signals for UI state**: `mode` (login/register tab), `loading`, `error`.
  Deliberate mix: ngModel wants plain fields; signals drive reactive UI.
- `submit()` picks the right service call by `mode()`, subscribes with
  `{ next, error }` — error maps `err.error.detail` (the API's Problem Details)
  into the banner.
- Pre-filled demo credentials + `fillDemo()` button — demo ergonomics.
- `@if (error(); )` / `@if (mode() === 'register')` — **new control flow**
  (Angular 17+): compiled to IIFEs, no view-children overhead, replaces `*ngIf`.

**Why not reactive forms?** "Two or four fields, static structure, no cross-field
validation — template-driven is less code and reads like the HTML. I'd switch to
reactive forms when the form is dynamic, has complex validation, or I need to
drive it programmatically."

### 6.2 `dashboard.ts` — the big one (492 lines)

**State — all signals:**
```ts
readonly employees = signal<Employee[]>([]);
readonly audit = signal<AuditEntry[]>([]);
readonly health = signal<Health | null>(null);
readonly expandedId = signal<string | null>(null);
readonly changes = signal<EmployeeChange[]>([]);
readonly changesLoading = signal(false);
// filters
readonly empSearch = signal('');  readonly empDepartment = signal('');  readonly empStatus = signal('');
readonly auditStatus = signal(''); readonly auditType = signal('');  readonly auditSearch = signal('');
readonly flashIds = signal<Set<string>>(new Set());
```

**Derived values** — arrow functions, not `computed()`:
```ts
readonly departments = () => [...new Set(this.employees().map(e => e.department))].sort();
readonly activeCount = () => this.employees().filter(e => e.isActive).length;
```
They re-run on every template read (cheap here). The more idiomatic form is
`computed(() => ...)` — read-only, cached until a dependency changes. **If asked
"why not computed?"** — honest answer: "these are cheap enough that plain getters
work; `computed` is the more idiomatic choice and what I'd use for anything
non-trivial." Don't oversell it.

**Lifecycle & polling:**
```ts
ngOnInit(): void {
  this.refresh();
  this.timer = setInterval(() => this.refresh(), 5000);
}
ngOnDestroy(): void { if (this.timer) clearInterval(this.timer); }
```
- `refresh()` fires 3 parallel calls (employees, audit, health), each
  `.subscribe({ next, error })`; errors are swallowed — the interceptor owns 401s,
  transient failures just mean "no update this tick."
- **Live-update detection (the "money shot" mechanic):**
  - `prevAuditIds: Set<number>` — any audit id not seen before → `flash('a:' + id)`.
  - `prevEmployeeUpdated: Map<string, string>` — employee whose `updatedAtUtc`
    moved (or is new) → `flash('e:' + id)`.
  - `auditLoaded` / `employeesLoaded` flags skip flashing on the first load.
  - `flash(key)` adds to `flashIds`, then a `setTimeout(1300ms)` removes it so the
    1.2s CSS animation (`.row-flash` in `styles.css`) can re-trigger later.
- **Why polling instead of WebSockets/SSE?** "The source of truth is a batch
  pipeline that lands events every ~15s; a 5s poll matches the data's actual
  cadence with zero server-side push infrastructure. For a real-time ops tool I'd
  use SSE — it's a one-line server change and keeps the same `refresh()` shape."

**Filters — server-side, composable:**
- Each control writes a signal and calls `refresh()` (which passes them as query
  params). `hasEmpFilters()` / `hasAuditFilters()` show the Clear button.
- Department dropdown options are derived from the current employee list.

**Expandable change history:**
- `toggleExpand(id)` — lazy-loads `employeeChanges(id)` only when a row opens;
  `changesLoading` drives the "Loading…" state; re-click collapses.

**Template idioms to be able to name:**
- `@for (e of employees(); track e.employeeId)` — `track` is the diffing key
  (like `trackBy`); `@empty` block for the no-data state.
- `[class.row-flash]="flashIds().has('e:' + e.employeeId)"` — class binding
  driven by a signal holding a Set.
- `{{ e.startDate | date:'mediumDate' }}`, `{{ e.baseSalary | number }}` — pipes
  (`DatePipe`, `DecimalPipe` imported in the component's `imports`).
- `(input)="onEmpSearch($any($event).target.value)"` — `$any()` is the template
  escape hatch for native DOM events; the typed alternative is
  `(input)="onEmpSearch($event.target.value)"` with the handler typed `(e: Event)`.
- `colspan="7"` expansion row inside the `@for` — the history renders as a second
  `<tr>` under the clicked row.

**Honest weakness:** it's one 492-line component. If asked how you'd refactor:
"Split into `employees-table` and `audit-log` child components with inputs/outputs
(or a shared `useChangeFlash`-style injection token), and move the polling into a
service. At this size it's readable; past ~3 panels I'd split."

---

## 7. Styling & build

### Tailwind v4
- `styles.css` starts with `@import "tailwindcss";` (v4's CSS-first entry — no
  `tailwind.config.js` needed) + the `.row-flash` keyframes.
- **The gotcha (in your Obsidian note, know it cold):** `@angular/build`'s
  application builder only reads `postcss.config.json` — a `.js`/`.cjs` PostCSS
  config is silently ignored, the `@tailwindcss/postcss` plugin never runs, and
  the app ships with **zero** utility classes (white page, raw text). Fix:
  `postcss.config.json` = `{ "plugins": { "@tailwindcss/postcss": {} } }`.
- Design language: 3D elevation — layered shadows (`shadow-lg shadow-slate-200/60`),
  gradient header, `hover:-translate-y-0.5` lift on stat cards. (Your stated
  preference: presence, not flat cards.)

### `angular.json`
- Builder: `@angular/build:application` (the new esbuild/vite-based builder,
  replaces the old `@angular-devkit/build-angular:browser`).
- `polyfills: ["zone.js"]` — zone.js is a polyfill, not an import.
- **Budgets** (production): initial bundle 500kB warn / 1MB error;
  any-component-style 4kB / 8kB — fail the build if a component's CSS bloats.
- `outputHashing: "all"` — cache-busting filenames in `dist/`.
- `defaultConfiguration: "production"` for build; `serve` defaults to development
  (sourcemaps, no minify).
- `serve.options.proxyConfig: "proxy.conf.json"`.

### `proxy.conf.json`
```json
{ "/auth": { "target": "http://localhost:5140", "secure": false, "changeOrigin": true }, ... }
```
- `ng serve` proxies API paths to the .NET host — **same-origin in dev, so no CORS
  and the Authorization header is never at risk from a cross-origin redirect.**
- `changeOrigin: true` rewrites the Host header to the target.
- CORS policy `"angular"` in `Program.cs` is the backup for non-proxied setups.
- **Related gotcha (know it):** `UseHttpsRedirection` was deliberately removed —
  VS's launch profile binds `https://localhost:7178`, and a 307 would bounce the
  proxy cross-origin, dropping the Authorization header → 401 on every call.

### `tsconfig.json` — the strictness story
- `strict: true`, `noImplicitOverride`, `noPropertyAccessFromIndexSignature`,
  `noImplicitReturns`, `noFallthroughCasesInSwitch`.
- `isolatedModules` + `module: "preserve"` (ESM-friendly, required by the new builder).
- `angularCompilerOptions`: `strictTemplates`, `strictInjectionParameters`,
  `strictInputAccessModifiers`, `typeCheckHostBindings` — the full Angular strict set.
- **Interview line:** "I run the strictest template + compiler settings Angular
  offers — type errors in templates are build errors, not runtime surprises."

---

## 8. Decision log — "why did you do it this way?"

| Decision | Why | Tradeoff / alternative |
|---|---|---|
| Signals for UI state | Fine-grained, no subscription bookkeeping, idiomatic v20 | Not for async streams — HttpClient is still RxJS |
| RxJS kept for HTTP | `HttpClient` is Observable-based; interceptors are Rx pipelines | Both coexist: signals = state, Rx = async |
| Template-driven forms | 2–4 static fields; less code | Reactive forms for dynamic/complex validation |
| Functional guard + interceptor | Plain functions, `inject()`, no classes | Class-based still works; functional is the current idiom |
| `providedIn: 'root'` | Tree-shakable singleton | Scoped providers for per-feature state |
| localStorage tokens | Survives refresh; simple | XSS-exposed; production answer = httpOnly Secure cookies + CSRF token |
| Hash location | Zero-config static hosting | Ugly URLs; path routing behind a real server |
| zone.js | Battle-tested CD | Zoneless is the future; opt-in, still maturing |
| 5s polling | Matches the 15s event cadence; no push infra | SSE/WebSocket for true real-time |
| Server-side filters | API does the work; composable params | Client filtering only for tiny static lists |
| Tailwind v4 | Fast, consistent, no CSS-in-JS runtime | Utility soup in templates; v4 CSS-first config |
| One big dashboard component | Readable at this size; demo deadline | Split into table components past ~3 panels |
| `@angular/build` application builder | Faster builds, modern pipeline | The PostCSS-config gotcha (JSON only) |

---

## 9. Likely Angular interview questions + model answers

1. **"Explain your 401 refresh flow."** → interceptor catches 401 → `auth.refresh()`
   rotates the pair (backend revokes old RT, issues new in same family) → replay
   request **with the freshly rotated token** → on refresh failure, clear session
   and let the guard bounce to login. Concurrent 401s share one in-flight refresh
   (dedup in `AuthService`), so exactly one rotation happens.
2. **"Signals vs RxJS — when do you use which?"** → Signals for synchronous
   reactive state (UI, auth flags); RxJS for async sequences (HTTP, timers).
   They interop: `toSignal`, `toObservable`.
3. **"Why zone.js?"** → automatic CD triggers from patched async APIs;
   `eventCoalescing` reduces redundant passes. Zoneless is the direction but I
   chose the stable path for a demo that must not glitch.
4. **"What's new in Angular 17–20 you actually use?"** → control flow
   (`@if`/`@for`/`@empty` with `track`), signals, `computed`, functional
   router/interceptor APIs, `inject()`, the `@angular/build` builder,
   `provideBrowserGlobalErrorListeners`.
5. **"How does the dashboard update live?"** → 5s poll → diff against previous
   ids/`updatedAtUtc` → flash changed rows via a signal-held Set + CSS animation.
6. **"How would you test this?"** → Karma/Jasmine is already configured
   (`ng test`); `TestBed` + `HttpClientTestingModule` to stub the API; test the
   interceptor's 401→refresh→replay and the guard's redirect. *(There are no spec
   files yet — see fix list.)*
7. **"Why hash routing?"** → static-host simplicity; I'd move to path routing
   with an SPA fallback behind IIS/nginx in production.
8. **"Tokens in localStorage — isn't that an XSS risk?"** → Yes, it's the
   standard tradeoff for a demo; the production answer is httpOnly Secure cookies
   for the refresh token (interceptor flow unchanged) + CSRF protection.
9. **"What's `track` in `@for`?"** → the identity key for diffing (like `trackBy`);
   without it Angular falls back to index tracking and can mis-associate rows.
10. **"What are the build budgets for?"** → fail the build if the initial bundle
    or a component's CSS exceeds limits — keeps the dashboard light by contract.

---

## 10. Honest weaknesses (know them before they find them)

1. **No frontend tests** — Karma/Jasmine is configured (`ng test`) but there are
   zero spec files. The README now says exactly that.
2. **No debounce on search** — every keystroke fires a server round-trip.
3. **One 492-line component** — fine, but have the refactor story ready.
4. **Arrow-function derived values instead of `computed()`** — defensible, but
   know the idiomatic form.

*(Fixed 2026-09-11: stale-token replay in the interceptor, concurrent-refresh
race, README port/Jest/SSR drift, dead `http` injection in the interceptor.)*

---

## 11. Fix list (before 9/16)

Done (2026-09-11, verified with `ng build` — 0 errors):
1. ✅ **Stale-token replay** fixed in `auth.interceptor.ts` (§4.3).
2. ✅ **README** fixed: port `5080` → `5140`; "Jest" → "Karma/Jasmine (configured,
   no specs yet)"; "SSR" removed from the architecture box.
3. ✅ **In-flight refresh dedup** in `auth.service.ts` (§4.4).
4. ✅ Dead `http` injection removed from the interceptor.

Remaining (optional):
- **Debounce search** — `debounceTime(300)` on the filter signals, or a simple
  timestamp check in `refresh()`.
- **One spec file** — `auth.interceptor.spec.ts` proving 401 → refresh → replay
  with the fresh token. Makes "I test my Angular" true.

---

## 12. Self-test (do this out loud, 15 minutes)

Without looking:
- [ ] Trace a request from `dashboard.refresh()` → interceptor → API → back to a signal.
- [ ] Explain what happens when the access token expires mid-poll (all 3 requests).
- [ ] Name every provider in `app.config.ts` and why it's there.
- [ ] Explain the guard's return value and why it's a UrlTree.
- [ ] Explain `track` in `@for` and what breaks without it.
- [ ] Explain the `.row-flash` mechanic end-to-end (signal → class → CSS → timeout).
- [ ] Explain the PostCSS/Tailwind gotcha from memory.
- [ ] Answer "why not reactive forms?" and "why not WebSockets?" in one sentence each.
- [ ] Explain the stale-token replay bug, why the demo hid it, and the fix — from memory.
- [ ] Explain the concurrent-refresh race and why in-flight dedup fixes it.

If you can do all of those cold, the Angular side is interview-ready.
