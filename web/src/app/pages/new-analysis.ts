import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';

type Step = { file: File; state: 'queued' | 'running' | 'done' | 'failed'; note?: string };
const OK = ['.pdf', '.docx', '.txt'];
const MAX = 5 * 1024 * 1024;

@Component({
  selector: 'app-new-analysis',
  imports: [FormsModule],
  template: `
    <div class="page">
      <h1>New analysis</h1>
      <p class="muted">Add a job description and up to 10 CVs (PDF, DOCX or TXT, max 5 MB each).</p>

      <div class="card">
        <h2>1. Job description</h2>
        <div class="field"><label for="rt">Role title</label><input id="rt" [(ngModel)]="roleTitle" maxlength="200" placeholder="e.g. Senior .NET Developer" [disabled]="busy()" /></div>
        <div class="field">
          <label for="jd">Paste the job description</label>
          <textarea id="jd" [(ngModel)]="jdText" [disabled]="busy() || !!jdFile()" placeholder="Paste the full job description here…"></textarea>
        </div>
        <div class="row">
          <input #jdf type="file" hidden accept=".pdf,.docx,.txt" (change)="pickJd($event)" />
          <button class="btn ghost sm" type="button" (click)="jdf.click()" [disabled]="busy()">…or upload a JD file</button>
          @if (jdFile(); as f) { <span class="chip">{{ f.name }}</span><button class="btn ghost sm" (click)="jdFile.set(null)" [disabled]="busy()">Remove</button> }
        </div>
      </div>

      <div class="card">
        <h2>2. CVs <span class="muted small">({{ steps().length }}/10)</span></h2>
        <div class="drop" [class.over]="over()" (dragover)="$event.preventDefault(); over.set(true)" (dragleave)="over.set(false)" (drop)="drop($event)">
          <input #cvf type="file" hidden multiple accept=".pdf,.docx,.txt" (change)="pickCvs($event)" />
          <p>Drag &amp; drop CVs here or <button class="btn ghost sm" type="button" (click)="cvf.click()" [disabled]="busy()">browse files</button></p>
        </div>
        @if (problem()) { <div class="error" role="alert">{{ problem() }}</div> }
        @if (steps().length) {
          <table aria-label="CV upload progress">
            <thead><tr><th>File</th><th>Size</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (s of steps(); track s.file; let i = $index) {
                <tr>
                  <td>{{ s.file.name }}</td>
                  <td>{{ (s.file.size / 1024).toFixed(0) }} KB</td>
                  <td>
                    @switch (s.state) {
                      @case ('queued') { <span class="muted">Queued</span> }
                      @case ('running') { <span class="spinner"></span> Analysing… }
                      @case ('done') { <span class="chip ok">Done</span> }
                      @case ('failed') { <span class="chip miss" [title]="s.note ?? ''">Failed</span> <span class="small muted">{{ s.note }}</span> }
                    }
                  </td>
                  <td>@if (!busy()) { <button class="btn ghost sm" (click)="remove(i)" aria-label="Remove CV">✕</button> }</td>
                </tr>
              }
            </tbody>
          </table>
          @if (busy()) { <div class="bar" role="progressbar" [attr.aria-valuenow]="progress()"><div [style.width.%]="progress()"></div></div> }
        }
      </div>

      @if (error()) { <div class="error" role="alert">{{ error() }}</div> }
      <button class="btn" (click)="run()" [disabled]="!canRun()">@if (busy()) { <span class="spinner"></span> Analysing… } @else { Analyse {{ steps().length || '' }} CV{{ steps().length === 1 ? '' : 's' }} }</button>
    </div>`,
  styles: `
    .drop{border:2px dashed var(--border);border-radius:12px;padding:22px;text-align:center;margin-bottom:14px;transition:.2s}
    .drop.over{border-color:var(--accent);background:var(--surface-2)} .drop p{margin:0;color:var(--muted)}
    .bar{height:8px;background:var(--track);border-radius:99px;margin-top:14px;overflow:hidden}
    .bar div{height:100%;background:linear-gradient(90deg,var(--brand-2),var(--accent));transition:width .4s}`,
})
export class NewAnalysisPage {
  private api = inject(ApiService);
  private router = inject(Router);
  roleTitle = ''; jdText = '';
  jdFile = signal<File | null>(null);
  steps = signal<Step[]>([]);
  over = signal(false); busy = signal(false);
  problem = signal(''); error = signal('');
  progress = computed(() => { const s = this.steps(); return s.length ? Math.round(100 * s.filter(x => x.state === 'done' || x.state === 'failed').length / s.length) : 0; });
  canRun = () => !this.busy() && this.roleTitle.trim().length > 0 && (this.jdText.trim().length > 0 || !!this.jdFile()) && this.steps().length > 0;

  pickJd(e: Event) {
    const f = (e.target as HTMLInputElement).files?.[0];
    if (f && this.valid(f)) this.jdFile.set(f);
    (e.target as HTMLInputElement).value = '';
  }
  pickCvs(e: Event) { this.add(Array.from((e.target as HTMLInputElement).files ?? [])); (e.target as HTMLInputElement).value = ''; }
  drop(e: DragEvent) { e.preventDefault(); this.over.set(false); this.add(Array.from(e.dataTransfer?.files ?? [])); }
  remove(i: number) { this.steps.update(s => s.filter((_, j) => j !== i)); }

  private valid(f: File): boolean {
    const ext = f.name.substring(f.name.lastIndexOf('.')).toLowerCase();
    if (!OK.includes(ext)) { this.problem.set(`${f.name}: only PDF, DOCX or TXT files are allowed.`); return false; }
    if (f.size > MAX) { this.problem.set(`${f.name}: larger than 5 MB.`); return false; }
    if (f.size === 0) { this.problem.set(`${f.name}: file is empty.`); return false; }
    return true;
  }

  private add(files: File[]) {
    this.problem.set('');
    const next = [...this.steps()];
    for (const f of files) {
      if (next.length >= 10) { this.problem.set('You can add at most 10 CVs.'); break; }
      if (this.valid(f)) next.push({ file: f, state: 'queued' });
    }
    this.steps.set(next);
  }

  private setState(i: number, state: Step['state'], note?: string) {
    this.steps.update(s => s.map((x, j) => (j === i ? { ...x, state, note } : x)));
  }

  run() {
    this.busy.set(true); this.error.set('');
    this.steps.update(s => s.map(x => ({ ...x, state: 'queued' as const, note: undefined })));
    this.api.createAnalysis(this.roleTitle.trim(), this.jdText, this.jdFile()).subscribe({
      next: a => this.uploadNext(a.id, 0),
      error: e => { this.busy.set(false); this.error.set(e.userMessage); },
    });
  }

  private uploadNext(id: string, i: number) {
    if (i >= this.steps().length) {
      this.busy.set(false);
      if (this.steps().some(s => s.state === 'done')) this.router.navigate(['/analyses', id]);
      else this.error.set('No CV could be analysed. Check the errors above.');
      return;
    }
    this.setState(i, 'running');
    this.api.addCv(id, this.steps()[i].file).subscribe({
      next: r => { r.status === 'Completed' ? this.setState(i, 'done') : this.setState(i, 'failed', r.error); this.uploadNext(id, i + 1); },
      error: e => { this.setState(i, 'failed', e.userMessage); this.uploadNext(id, i + 1); },
    });
  }
}
