import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { AuthResponse, User } from './models';

const ACCESS_KEY = 'wfs.accessToken';
const REFRESH_KEY = 'wfs.refreshToken';
const USER_KEY = 'wfs.user';

/**
 * Holds the JWT access + refresh tokens and the current user.
 * Uses signals for reactive state; persists tokens to localStorage so a
 * page refresh keeps the session.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  readonly user = signal<User | null>(this.readUser());
  readonly isAuthenticated = signal<boolean>(!!this.readUser());

  private readUser(): User | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  }

  get accessToken(): string | null {
    return localStorage.getItem(ACCESS_KEY);
  }

  get refreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  login(email: string, password: string): Observable<User> {
    return this.http
      .post<AuthResponse>('/auth/login', { email, password })
      .pipe(map((res) => this.storeSession(res)));
  }

  register(
    email: string,
    firstName: string,
    lastName: string,
    password: string
  ): Observable<User> {
    return this.http
      .post<AuthResponse>('/auth/register', { email, firstName, lastName, password })
      .pipe(map((res) => this.storeSession(res)));
  }

  /**
   * Exchanges the stored refresh token for a fresh access token.
   * The API rotates the refresh token on every use, so we persist the new one.
   */
  refresh(): Observable<User> {
    const rt = this.refreshToken;
    if (!rt) {
      return throwError(() => new Error('No refresh token'));
    }
    return this.http
      .post<AuthResponse>('/auth/refresh', { refreshToken: rt })
      .pipe(map((res) => this.storeSession(res)));
  }

  logout(): void {
    const rt = this.refreshToken;
    if (rt) {
      this.http.post('/auth/logout', { refreshToken: rt }).subscribe({
        error: () => {
          /* best-effort; clear local state regardless */
        },
      });
    }
    this.clearSession();
  }

  private storeSession(res: AuthResponse): User {
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    this.user.set(res.user);
    this.isAuthenticated.set(true);
    return res.user;
  }

  private clearSession(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this.user.set(null);
    this.isAuthenticated.set(false);
  }
}
