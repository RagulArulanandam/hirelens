import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly message = signal<string | null>(null);
  private timer?: ReturnType<typeof setTimeout>;
  show(msg: string) {
    this.message.set(msg);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.message.set(null), 6000);
  }
}

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token;
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};

/** Turns ProblemDetails / network failures into readable messages; logs out on expired sessions. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(ToastService);
  return next(req).pipe(catchError((e: HttpErrorResponse) => {
    let msg = 'Something went wrong. Please try again.';
    if (e.status === 0) msg = 'Cannot reach the server.';
    else if (e.status === 429) msg = 'Too many requests. Wait a minute and retry.';
    else if (e.error?.errors) msg = Object.values(e.error.errors as Record<string, string[]>).flat().join(' ');
    else if (e.error?.detail || e.error?.title) msg = e.error.detail ?? e.error.title;
    if (e.status === 401 && !req.url.endsWith('/auth/login')) { auth.logout(); msg = 'Session expired. Please sign in again.'; }
    if (!req.url.endsWith('/auth/login') && e.status !== 401) toast.show(msg);
    return throwError(() => Object.assign(e, { userMessage: msg }));
  }));
};

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? true : inject(Router).createUrlTree(['/login']);
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAdmin() ? true : inject(Router).createUrlTree(['/analyses/new']);
};
