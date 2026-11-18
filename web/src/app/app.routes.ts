import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/http';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.LoginPage), title: 'Sign in – HireLens' },
  { path: 'analyses/new', canActivate: [authGuard], loadComponent: () => import('./pages/new-analysis').then(m => m.NewAnalysisPage), title: 'New analysis – HireLens' },
  { path: 'analyses/:id', canActivate: [authGuard], loadComponent: () => import('./pages/results').then(m => m.ResultsPage), title: 'Results – HireLens' },
  { path: 'history', canActivate: [authGuard], loadComponent: () => import('./pages/history').then(m => m.HistoryPage), title: 'History – HireLens' },
  { path: 'admin', canActivate: [authGuard, adminGuard], loadComponent: () => import('./pages/admin').then(m => m.AdminPage), title: 'Admin – HireLens' },
  { path: '', pathMatch: 'full', redirectTo: 'analyses/new' },
  { path: '**', redirectTo: 'analyses/new' },
];
