import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AnalysisSummary } from '../core/models';
import { VerdictComponent } from '../shared/ui';

@Component({
  selector: 'app-history',
  imports: [FormsModule, DatePipe, RouterLink, VerdictComponent],
  template: `
    <div class="page">
      <div class="row between"><h1>History</h1><a routerLink="/analyses/new" class="btn sm">New analysis</a></div>
      <div class="card">
        <form class="row" (ngSubmit)="reload(1)">
          <div class="grow"><label for="s">Role title</label><input id="s" name="s" [(ngModel)]="search" placeholder="Search by role title" /></div>
          <div><label for="f">From</label><input id="f" name="f" type="date" [(ngModel)]="from" /></div>
          <div><label for="t">To</label><input id="t" name="t" type="date" [(ngModel)]="to" /></div>
          <button class="btn" type="submit" style="align-self:flex-end">Search</button>
        </form>
      </div>
      <div class="card">
        @if (loading()) { <p class="muted"><span class="spinner"></span> Loading…</p> }
        @else if (!items().length) { <p class="muted">No analyses found. <a routerLink="/analyses/new">Start one</a>.</p> }
        @else {
          <table aria-label="Past analyses">
            <thead><tr><th>Role</th><th>Date</th><th>CVs</th><th>Best fit</th><th></th></tr></thead>
            <tbody>
              @for (a of items(); track a.id) {
                <tr class="clickable" (click)="open(a.id)" tabindex="0" (keydown.enter)="open(a.id)">
                  <td><b>{{ a.roleTitle }}</b></td><td>{{ a.createdAt | date: 'medium' }}</td><td>{{ a.cvCount }}</td>
                  <td>@if (a.bestVerdict) { {{ a.bestFitScore }} <hl-verdict [verdict]="a.bestVerdict" /> } @else { — }</td>
                  <td><button class="btn danger sm" (click)="remove($event, a)" aria-label="Delete analysis">Delete</button></td>
                </tr>
              }
            </tbody>
          </table>
          <div class="row between" style="margin-top:14px">
            <span class="muted small">{{ total() }} result(s) · page {{ page() }} of {{ pages() }}</span>
            <div class="row">
              <button class="btn ghost sm" (click)="reload(page() - 1)" [disabled]="page() <= 1">Previous</button>
              <button class="btn ghost sm" (click)="reload(page() + 1)" [disabled]="page() >= pages()">Next</button>
            </div>
          </div>
        }
      </div>
    </div>`,
})
export class HistoryPage implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  search = ''; from = ''; to = '';
  items = signal<AnalysisSummary[]>([]);
  total = signal(0); page = signal(1); pages = signal(1); loading = signal(true);

  ngOnInit() { this.reload(1); }

  reload(p: number) {
    this.loading.set(true);
    this.api.list(this.search.trim(), this.from, this.to, p).subscribe({
      next: r => { this.items.set(r.items); this.total.set(r.total); this.page.set(r.page); this.pages.set(Math.max(1, Math.ceil(r.total / r.pageSize))); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  open(id: string) { this.router.navigate(['/analyses', id]); }

  remove(e: Event, a: AnalysisSummary) {
    e.stopPropagation();
    if (!confirm(`Delete "${a.roleTitle}" and its uploaded CV files? This cannot be undone.`)) return;
    this.api.delete(a.id).subscribe(() => this.reload(this.items().length === 1 && this.page() > 1 ? this.page() - 1 : this.page()));
  }
}
