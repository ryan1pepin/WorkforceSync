import { Routes, CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { Login } from './login';
import { Dashboard } from './dashboard';

/**
 * Guards the dashboard: a stored session is only trusted once it has been
 * confirmed against the server (GET /auth/me). This means a session left in
 * localStorage from a previous run — e.g. before an API restart that
 * invalidated all tokens — is bounced to the login screen instead of letting
 * the user land on a dead, empty dashboard.
 */
const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.validateSession().pipe(
    map((ok) => (ok ? true : router.parseUrl('/login'))),
  );
};

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' },
];
