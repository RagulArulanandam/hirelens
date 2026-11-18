import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API, AuthUser } from './models';

const KEY = 'hirelens.auth';

function load(): AuthUser | null {
  try {
    const u = JSON.parse(localStorage.getItem(KEY) ?? 'null') as AuthUser | null;
    return u && new Date(u.expires) > new Date() ? u : null;
  } catch { return null; }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  readonly user = signal<AuthUser | null>(load());
  readonly isLoggedIn = computed(() => this.user() !== null);
  readonly isAdmin = computed(() => this.user()?.roles.includes('Admin') ?? false);
  get token() { return this.user()?.token ?? null; }

  login(email: string, password: string) {
    return this.http.post<AuthUser>(`${API}/auth/login`, { email, password }).pipe(
      tap(u => { this.user.set(u); try { localStorage.setItem(KEY, JSON.stringify(u)); } catch { /* storage blocked */ } }));
  }

  logout() {
    this.user.set(null);
    try { localStorage.removeItem(KEY); } catch { /* ignore */ }
    this.router.navigate(['/login']);
  }
}
