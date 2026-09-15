import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

const DEMO_USER = {
  id: 1,
  email: 'demo@corp.example',
  firstName: 'Demo',
  lastName: 'User',
  createdAtUtc: '2026-01-01T00:00:00Z',
};

const OLD_ACCESS = 'old-access-token';
const OLD_REFRESH = 'old-refresh-token';
const NEW_ACCESS = 'new-access-token';
const NEW_REFRESH = 'new-refresh-token';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('wfs.accessToken', OLD_ACCESS);
    localStorage.setItem('wfs.refreshToken', OLD_REFRESH);
    localStorage.setItem('wfs.user', JSON.stringify(DEMO_USER));

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    backend.verify();
    localStorage.clear();
  });

  it('attaches the Bearer token to outgoing requests', () => {
    http.get('/employees').subscribe();

    const req = backend.expectOne('/employees');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${OLD_ACCESS}`);
    req.flush({ items: [] });
  });

  it('on 401, refreshes and replays the request with the FRESH token', () => {
    let result: unknown;
    http.get('/employees').subscribe({
      next: (res) => (result = res),
      error: (err) => fail(err),
    });

    // 1st attempt goes out with the (now expired) stored token…
    const first = backend.expectOne('/employees');
    expect(first.request.headers.get('Authorization')).toBe(`Bearer ${OLD_ACCESS}`);
    first.flush(null, { status: 401, statusText: 'Unauthorized' });

    // …the interceptor exchanges the refresh token for a fresh pair…
    const refresh = backend.expectOne('/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: OLD_REFRESH });
    refresh.flush({
      user: DEMO_USER,
      accessToken: NEW_ACCESS,
      refreshToken: NEW_REFRESH,
    });

    // …and replays the ORIGINAL request carrying the freshly rotated token.
    const replay = backend.expectOne('/employees');
    expect(replay.request.headers.get('Authorization')).toBe(`Bearer ${NEW_ACCESS}`);
    replay.flush({ items: [] });

    expect(result).toEqual({ items: [] });
    // The rotated pair is persisted, so a later request uses the new token.
    expect(localStorage.getItem('wfs.accessToken')).toBe(NEW_ACCESS);
    expect(localStorage.getItem('wfs.refreshToken')).toBe(NEW_REFRESH);
  });

  it('on refresh failure, clears the session and navigates to /login', () => {
    const navSpy = spyOn(router, 'navigateByUrl').and.resolveTo(true);
    let failed = false;
    http.get('/employees').subscribe({
      error: () => (failed = true),
    });

    const first = backend.expectOne('/employees');
    first.flush(null, { status: 401, statusText: 'Unauthorized' });

    const refresh = backend.expectOne('/auth/refresh');
    refresh.flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBeTrue();
    // Session is cleared locally…
    expect(localStorage.getItem('wfs.accessToken')).toBeNull();
    expect(localStorage.getItem('wfs.refreshToken')).toBeNull();
    // …and the user is sent back to the login screen.
    expect(navSpy).toHaveBeenCalledWith('/login');
    // logout() fires a best-effort server-side revocation.
    backend.expectOne('/auth/logout').flush(null, { status: 200, statusText: 'OK' });
  });

  it('passes non-401 errors through untouched (no refresh attempt)', () => {
    let status: number | undefined;
    http.get('/employees').subscribe({
      error: (err) => (status = err.status),
    });

    const req = backend.expectOne('/employees');
    req.flush(null, { status: 500, statusText: 'Internal Server Error' });

    expect(status).toBe(500);
    // Session must be intact — a server error is not an auth failure.
    expect(localStorage.getItem('wfs.accessToken')).toBe(OLD_ACCESS);
  });
});
