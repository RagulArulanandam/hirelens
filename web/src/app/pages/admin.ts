import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { ToastService } from '../core/http';
import { UsageStats, UserRow } from '../core/models';

@Component({
  selector: 'app-admin',
  imports: [FormsModule, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <h1>Admin</h1>
      @if (stats(); as s) {
        <div class="grid4">
          <div class="card"><div class="muted small">Analyses</div><div class="stat">{{ s.analyses }}</div></div>
          <div class="card"><div class="muted small">CVs analysed</div><div class="stat">{{ s.cvsAnalysed }}</div></div>
          <div class="card"><div class="muted small">Tokens used</div><div class="stat">{{ s.inputTokens + s.outputTokens | number }}</div><div class="muted small">{{ s.inputTokens | number }} in · {{ s.outputTokens | number }} out</div></div>
          <div class="card"><div class="muted small">Cache hit rate</div><div class="stat">{{ s.cacheHitRate }}%</div><div class="muted small">{{ s.cacheHits }} free re-runs</div></div>
        </div>
      }

      <div class="grid2">
        <div class="card">
          <h2>AI model</h2>
          <p class="muted small">Provider: <b>{{ provider() }}</b>. @if (provider() === 'Mock') { No API key is configured, so the mock scorer is used and this setting has no effect. }</p>
          <div class="field">
            <label for="m">Default model</label>
            <select id="m" [(ngModel)]="model">
              <option value="claude-haiku-5-5">claude-haiku-5-5 (fast, cheap)</option>
              <option value="claude-sonnet-5-5">claude-sonnet-5-5 (deeper analysis)</option>
            </select>
          </div>
          <button class="btn sm" (click)="saveModel()">Save model</button>
        </div>
        <div class="card">
          <h2>Data privacy</h2>
          <p class="muted small">Uploaded CV files are deleted automatically after this many days. Scores are kept. 0 = keep files.</p>
          <div class="field"><label for="r">Auto-delete CV files after (days)</label><input id="r" type="number" min="0" max="3650" [(ngModel)]="retention" /></div>
          <button class="btn sm" (click)="saveRetention()">Save retention</button>
        </div>
      </div>

      <div class="card">
        <h2>Users</h2>
        <form class="row" (ngSubmit)="addUser()" autocomplete="off">
          <div class="grow"><label for="un">Full name</label><input id="un" name="n" [(ngModel)]="nu.fullName" required /></div>
          <div class="grow"><label for="ue">Email</label><input id="ue" name="e" type="email" [(ngModel)]="nu.email" required /></div>
          <div class="grow"><label for="up">Password (10+ chars)</label><input id="up" name="p" type="password" autocomplete="new-password" [(ngModel)]="nu.password" required /></div>
          <div><label for="ur">Role</label><select id="ur" name="r" [(ngModel)]="nu.role"><option>HR</option><option>Admin</option></select></div>
          <button class="btn" type="submit" style="align-self:flex-end" [disabled]="!nu.email || !nu.fullName || nu.password.length < 10">Add user</button>
        </form>
        <table style="margin-top:16px" aria-label="Users">
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Created</th><th></th></tr></thead>
          <tbody>
            @for (u of users(); track u.id) {
              <tr><td>{{ u.fullName }}</td><td>{{ u.email }}</td><td><span class="chip">{{ u.roles.join(', ') }}</span></td><td>{{ u.createdAt | date: 'mediumDate' }}</td>
                <td><button class="btn danger sm" (click)="remove(u)">Delete</button></td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>`,
  styles: `.grid4{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:20px}.grid4 .card{margin-bottom:20px}`,
})
export class AdminPage implements OnInit {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  stats = signal<UsageStats | null>(null);
  users = signal<UserRow[]>([]);
  provider = signal('');
  model = 'claude-haiku-5-5'; retention = 30;
  nu = { fullName: '', email: '', password: '', role: 'HR' };

  ngOnInit() {
    this.api.stats().subscribe(s => this.stats.set(s));
    this.api.users().subscribe(u => this.users.set(u));
    this.api.getSettings().subscribe(s => { this.model = s.defaultModel; this.retention = s.retentionDays; this.provider.set(s.provider); });
  }

  saveModel() { this.api.setModel(this.model).subscribe(() => this.note('Model saved')); }
  saveRetention() { this.api.setRetention(Number(this.retention)).subscribe(() => this.note('Retention saved')); }

  addUser() {
    this.api.createUser(this.nu).subscribe(u => { this.users.update(l => [...l, u]); this.nu = { fullName: '', email: '', password: '', role: 'HR' }; this.note('User created'); });
  }

  remove(u: UserRow) {
    if (!confirm(`Delete user ${u.email}?`)) return;
    this.api.deleteUser(u.id).subscribe(() => this.users.update(l => l.filter(x => x.id !== u.id)));
  }

  private note(m: string) { this.toast.show(m); }
}
