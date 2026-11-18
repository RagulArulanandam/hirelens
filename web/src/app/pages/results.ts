import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AnalysisDetail, CvResult } from '../core/models';
import { ScoreRingComponent, VerdictComponent } from '../shared/ui';

@Component({
  selector: 'app-results',
  imports: [DatePipe, RouterLink, ScoreRingComponent, VerdictComponent],
  template: `
    <div class="page">
      @if (!data()) {
        <p class="muted"><span class="spinner"></span> Loading…</p>
      } @else {
        <div class="row between">
          <div>
            <h1>{{ data()!.roleTitle }}</h1>
            <p class="muted small">{{ data()!.createdAt | date: 'medium' }} · {{ data()!.results.length }} CV(s)</p>
          </div>
          <div class="row no-print">
            <a routerLink="/analyses/new" class="btn ghost sm">New analysis</a>
            <button class="btn ghost sm" (click)="print()">Print</button>
            <button class="btn sm" (click)="exportPdf()" [disabled]="exporting()">Export PDF</button>
            <button class="btn danger sm" (click)="remove()">Delete</button>
          </div>
        </div>

        @if (data()!.results.length > 1) {
          <div class="card">
            <h2>Ranking</h2>
            <table aria-label="Ranked CVs">
              <thead><tr><th>#</th><th>Candidate file</th><th>Fit</th><th>ATS</th><th>CV</th><th>Verdict</th></tr></thead>
              <tbody>
                @for (r of data()!.results; track r.id; let i = $index) {
                  <tr class="clickable" [class.selected]="r.id === sel()?.id" (click)="selectedId.set(r.id)" tabindex="0" (keydown.enter)="selectedId.set(r.id)">
                    <td>{{ i + 1 }}</td><td>{{ r.fileName }}</td>
                    @if (r.status === 'Completed') {
                      <td><b>{{ r.fitScore }}</b></td><td>{{ r.atsScore }}</td><td>{{ r.cvScore }}</td><td><hl-verdict [verdict]="r.verdict" /></td>
                    } @else { <td colspan="4" class="muted">Failed: {{ r.error }}</td> }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        @if (sel(); as r) {
          @if (r.status !== 'Completed' || !r.output) {
            <div class="card"><div class="error">{{ r.fileName }} could not be analysed: {{ r.error }}</div></div>
          } @else {
            @let o = r.output;
            <div class="card">
              <div class="row between"><h2>{{ r.fileName }}</h2><hl-verdict [verdict]="o.fitVerdict" /></div>
              <div class="rings">
                <hl-score-ring [value]="o.atsScore" label="ATS score" />
                <hl-score-ring [value]="o.cvScore" label="CV quality" />
                <hl-score-ring [value]="o.fitScore" label="Fit score" />
              </div>
              <p class="summary">{{ o.summary }}</p>
              <p class="muted small">
                Model: {{ r.model }} · @if (r.fromCache) { served from cache (0 tokens) } @else { {{ r.inputTokens + r.outputTokens }} tokens }
              </p>
            </div>

            <div class="grid2">
              <div class="card">
                <h3>Matched skills</h3>
                <div class="chips">@for (s of o.matchedSkills; track s) { <span class="chip ok">{{ s }}</span> } @empty { <span class="muted">None found</span> }</div>
              </div>
              <div class="card">
                <h3>Missing skills</h3>
                <div class="chips">@for (s of o.missingSkills; track s) { <span class="chip miss">{{ s }}</span> } @empty { <span class="muted">Nothing missing</span> }</div>
              </div>
            </div>

            <div class="card">
              <h2>Gaps to improve</h2>
              @if (o.gaps.length) {
                <table aria-label="Gaps">
                  <thead><tr><th>Area</th><th>Detail</th><th>Suggestion</th></tr></thead>
                  <tbody>@for (g of o.gaps; track g.area + g.detail) { <tr><td><b>{{ g.area }}</b></td><td>{{ g.detail }}</td><td>{{ g.suggestion }}</td></tr> }</tbody>
                </table>
              } @else { <p class="muted">No significant gaps.</p> }
            </div>

            <div class="grid2">
              <div class="card"><h3>Strengths</h3><ul>@for (s of o.strengths; track s) { <li>{{ s }}</li> } @empty { <li class="muted">—</li> }</ul></div>
              <div class="card"><h3>Red flags</h3><ul>@for (s of o.redFlags; track s) { <li>{{ s }}</li> } @empty { <li class="muted">None</li> }</ul></div>
            </div>

            @if (o.atsBreakdown; as b) {
              <div class="card">
                <h2>How the ATS score was computed</h2>
                <p class="muted small">Computed deterministically, without AI, so it is repeatable and explainable.</p>
                <table aria-label="ATS breakdown">
                  <thead><tr><th>Check</th><th>Result</th><th>Points</th></tr></thead>
                  <tbody>
                    <tr><td><b>Keyword coverage</b></td><td>{{ b.matchedKeywords.length }} of {{ b.jdKeywords.length }} JD keywords ({{ b.keywordCoveragePct }}%)</td><td>{{ b.keywordPoints }} / 60</td></tr>
                    @for (c of b.checks; track c.name) {
                      <tr><td>{{ c.name }}</td><td><span class="chip" [class.ok]="c.passed" [class.miss]="!c.passed">{{ c.passed ? 'Pass' : 'Fail' }}</span> <span class="muted small">{{ c.note }}</span></td><td>{{ c.points }} / {{ c.maxPoints }}</td></tr>
                    }
                    <tr><td><b>Total</b></td><td></td><td><b>{{ b.score }} / 100</b></td></tr>
                  </tbody>
                </table>
              </div>
            }
          }
        }
      }
    </div>`,
  styles: `
    .rings{display:flex;gap:40px;justify-content:center;flex-wrap:wrap;margin:18px 0}
    .summary{font-size:1.02rem;text-align:center;max-width:46rem;margin:0 auto 10px}
    ul{margin:0;padding-left:1.2rem}`,
})
export class ResultsPage implements OnInit {
  id = input.required<string>();
  private api = inject(ApiService);
  private router = inject(Router);
  data = signal<AnalysisDetail | null>(null);
  selectedId = signal<string | null>(null);
  exporting = signal(false);
  sel = computed<CvResult | undefined>(() => {
    const d = this.data();
    return d?.results.find(r => r.id === this.selectedId()) ?? d?.results[0];
  });

  ngOnInit() { this.api.get(this.id()).subscribe({ next: d => this.data.set(d), error: () => this.router.navigate(['/history']) }); }
  print() { window.print(); }

  exportPdf() {
    this.exporting.set(true);
    this.api.exportPdf(this.id()).subscribe({
      next: blob => {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = `HireLens-${this.data()!.roleTitle.replace(/[^\w-]+/g, '_')}.pdf`;
        a.click(); URL.revokeObjectURL(a.href); this.exporting.set(false);
      },
      error: () => this.exporting.set(false),
    });
  }

  remove() {
    if (!confirm('Delete this analysis and its uploaded CV files? This cannot be undone.')) return;
    this.api.delete(this.id()).subscribe(() => this.router.navigate(['/history']));
  }
}
