import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

type Mode = 'login' | 'register';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-4">
      <div class="w-full max-w-md">
        <div class="text-center mb-8">
          <div class="text-4xl font-bold text-white tracking-tight">WorkforceSync</div>
          <p class="text-slate-400 mt-2">HR data integration platform</p>
        </div>

        <div class="bg-white rounded-2xl shadow-2xl p-8">
          <!-- Mode tabs -->
          <div class="flex rounded-lg bg-slate-100 p-1 mb-6">
            <button
              type="button"
              (click)="mode.set('login')"
              class="flex-1 rounded-md py-2 text-sm font-medium transition"
              [class]="mode() === 'login' ? 'bg-white shadow text-slate-800' : 'text-slate-500 hover:text-slate-700'"
            >
              Sign in
            </button>
            <button
              type="button"
              (click)="mode.set('register')"
              class="flex-1 rounded-md py-2 text-sm font-medium transition"
              [class]="mode() === 'register' ? 'bg-white shadow text-slate-800' : 'text-slate-500 hover:text-slate-700'"
            >
              Create account
            </button>
          </div>

          @if (error(); ) {
            <div class="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-4">
              {{ error() }}
            </div>
          }

          <form (ngSubmit)="submit()" class="space-y-4">
            @if (mode() === 'register') {
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-sm font-medium text-slate-600 mb-1">First name</label>
                  <input
                    type="text"
                    name="firstName"
                    [(ngModel)]="firstName"
                    required
                    class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    placeholder="Ada"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium text-slate-600 mb-1">Last name</label>
                  <input
                    type="text"
                    name="lastName"
                    [(ngModel)]="lastName"
                    required
                    class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    placeholder="Lovelace"
                  />
                </div>
              </div>
            }

            <div>
              <label class="block text-sm font-medium text-slate-600 mb-1">Email</label>
              <input
                type="email"
                name="email"
                [(ngModel)]="email"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="you@corp.example"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-600 mb-1">Password</label>
              <input
                type="password"
                name="password"
                [(ngModel)]="password"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="••••••••"
              />
            </div>

            <button
              type="submit"
              [disabled]="loading()"
              class="w-full rounded-lg bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-medium py-2.5 transition"
            >
              {{ loading() ? (mode() === 'login' ? 'Signing in…' : 'Creating account…') : (mode() === 'login' ? 'Sign in' : 'Create account') }}
            </button>
          </form>

          @if (mode() === 'login') {
            <div class="mt-6 rounded-lg bg-slate-50 border border-slate-200 px-3 py-2.5 text-xs text-slate-500">
              <span class="font-medium text-slate-600">Demo account:</span>
              <code class="text-slate-700">demo@corp.example</code> /
              <code class="text-slate-700">Demo123!</code>
              <button
                type="button"
                (click)="fillDemo()"
                class="ml-1 text-blue-600 hover:text-blue-700 font-medium"
              >
                fill
              </button>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class Login {
  private auth = inject(AuthService);
  private router = inject(Router);

  readonly mode = signal<Mode>('login');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  email = 'demo@corp.example';
  password = 'Demo123!';
  firstName = '';
  lastName = '';

  fillDemo(): void {
    this.email = 'demo@corp.example';
    this.password = 'Demo123!';
    this.error.set(null);
  }

  submit(): void {
    this.loading.set(true);
    this.error.set(null);

    const request =
      this.mode() === 'login'
        ? this.auth.login(this.email, this.password)
        : this.auth.register(this.email, this.firstName, this.lastName, this.password);

    request.subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.detail ?? err?.error?.message ?? 'Request failed. Check your details.');
      },
    });
  }
}
