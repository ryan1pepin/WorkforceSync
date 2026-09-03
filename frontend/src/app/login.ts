import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

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
          <h2 class="text-xl font-semibold text-slate-800 mb-6">Sign in</h2>

          <form (ngSubmit)="submit()" class="space-y-4">
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

            @if (error(); ) {
              <div class="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                {{ error() }}
              </div>
            }

            <button
              type="submit"
              [disabled]="loading()"
              class="w-full rounded-lg bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-medium py-2.5 transition"
            >
              {{ loading() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>

          <p class="text-xs text-slate-400 mt-6 text-center">
            Demo: register a new account, or use the seeded credentials.
          </p>
        </div>
      </div>
    </div>
  `,
})
export class Login {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = '';
  password = '';
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Sign in failed. Check your credentials.');
      },
    });
  }
}
