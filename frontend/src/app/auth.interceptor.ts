import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './auth.service';

/**
 * Attaches the Bearer token to every request. On a 401, attempts exactly one
 * refresh and replays the original request; if the refresh fails, the session
 * is cleared and the caller is bounced to login.
 */
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
        switchMap(() => next(authorized)),
        catchError((refreshErr: HttpErrorResponse) => {
          auth.logout();
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
