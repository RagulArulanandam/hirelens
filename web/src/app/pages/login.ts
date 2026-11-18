import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { LogoComponent } from '../shared/ui';

@Component({
  selector: 'app-login',
  imports: [FormsModule, LogoComponent],
  template: `
    <div class="wrap">
      <section class="hero">
        <hl-logo [size]="44" />
        <h1>See the fit before the interview.</h1>
        <p>Upload a job description and a stack of CVs. HireLens returns an explainable ATS score, a CV quality score, a fit verdict and the exact gaps to close.</p>
        <ul>
          <li>Explainable ATS scoring, not a black box</li>
          <li>Rank up to 10 CVs per role</li>
          <li>Your CVs stay private: no CV text in logs</li>
        </ul>
      </section>
      <form class="card form" (ngSubmit)="submit()">
        <h2>Sign in</h2>
        @if (error()) { <div class="error" role="alert">{{ error() }}</div> }
        <div class="field"><label for="e">Email</label><input id="e" name="email" type="email" autocomplete="username" [(ngModel)]="email" required /></div>
        <div class="field"><label for="p">Password</label><input id="p" name="password" type="password" autocomplete="current-password" [(ngModel)]="password" required /></div>
        <button class="btn" type="submit" [disabled]="busy() || !email || !password">@if (busy()) { <span class="spinner"></span> } Sign in</button>
      </form>
    </div>`,
  styles: `
    .wrap{min-height:100vh;display:grid;grid-template-columns:1.2fr 1fr;gap:40px;align-items:center;max-width:1050px;margin:0 auto;padding:30px 20px}
    .hero h1{font-size:2.4rem;margin:26px 0 12px;background:linear-gradient(135deg,var(--brand-2),var(--accent));-webkit-background-clip:text;background-clip:text;color:transparent}
    .hero p{color:var(--muted);font-size:1.05rem;max-width:34rem} .hero ul{color:var(--muted);padding-left:1.2rem}
    .form{max-width:420px;width:100%;justify-self:center;margin:0}
    @media(max-width:780px){.wrap{grid-template-columns:1fr;min-height:auto;gap:20px}.hero h1{font-size:1.8rem}}`,
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  email = ''; password = '';
  busy = signal(false);
  error = signal('');

  constructor() { if (this.auth.isLoggedIn()) this.router.navigate(['/analyses/new']); }

  submit() {
    this.busy.set(true); this.error.set('');
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/analyses/new']),
      error: e => { this.busy.set(false); this.error.set(e.status === 401 ? 'Invalid email or password.' : e.userMessage); },
    });
  }
}
