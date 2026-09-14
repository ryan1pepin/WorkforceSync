import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';

/**
 * Attaches the Bearer token to every request. On a 401, attempts exactly one
 * refresh and replays the original request with the rotated token; if the
 * refresh fails, the session is cleared and the caller is bounced to login.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

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
        switchMap(() => {
          // Re-read the token AFTER rotation: `authorized` still carries the
          // expired one, and replaying it would 401 again.
          const fresh = auth.accessToken;
          const replay = fresh
            ? req.clone({ setHeaders: { Authorization: `Bearer ${fresh}` } })
            : req;
          return next(replay);
        }),
        catchError((refreshErr: HttpErrorResponse) => {
          auth.logout();
          // Session is dead (e.g. the API restarted and invalidated all
          // tokens) — send the user back to the login screen.
          if (router.url !== '/login') {
            router.navigateByUrl('/login');
          }
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
