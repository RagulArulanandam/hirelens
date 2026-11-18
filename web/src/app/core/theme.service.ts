import { Injectable, effect, signal } from '@angular/core';

type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.initial());

  constructor() {
    effect(() => {
      document.documentElement.setAttribute('data-theme', this.theme());
      try { localStorage.setItem('hirelens.theme', this.theme()); } catch { /* ignore */ }
    });
  }

  toggle() { this.theme.update(t => (t === 'dark' ? 'light' : 'dark')); }

  private initial(): Theme {
    try {
      const s = localStorage.getItem('hirelens.theme');
      if (s === 'light' || s === 'dark') return s;
    } catch { /* ignore */ }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
