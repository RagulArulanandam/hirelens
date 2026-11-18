import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'hl-logo',
  template: `
    <svg [attr.height]="size()" viewBox="0 0 190 48" role="img" aria-label="HireLens" xmlns="http://www.w3.org/2000/svg">
      <rect x="4" y="5" width="26" height="34" rx="4" fill="none" stroke="var(--brand)" stroke-width="3"/>
      <path d="M10 14h14M10 20h14M10 26h8" stroke="var(--brand)" stroke-width="2.5" stroke-linecap="round"/>
      <circle cx="30" cy="30" r="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="3.5"/>
      <path d="M26 30l3 3 5-6" fill="none" stroke="var(--accent)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M37.5 37.5L45 45" stroke="var(--accent)" stroke-width="4" stroke-linecap="round"/>
      <text x="56" y="32" font-family="Inter, Segoe UI, system-ui, sans-serif" font-size="24" font-weight="700" fill="var(--text)">Hire<tspan fill="var(--accent)">Lens</tspan></text>
    </svg>`,
  styles: ':host{display:inline-flex}',
})
export class LogoComponent { size = input(36); }

@Component({
  selector: 'hl-score-ring',
  template: `
    <div class="ring" [attr.aria-label]="label() + ' ' + value()">
      <svg viewBox="0 0 120 120">
        <circle cx="60" cy="60" r="52" class="track"/>
        <circle cx="60" cy="60" r="52" class="bar" [style.stroke]="color()"
          [style.stroke-dasharray]="C" [style.stroke-dashoffset]="offset()" transform="rotate(-90 60 60)"/>
      </svg>
      <div class="num">{{ value() }}<small>/100</small></div>
    </div>
    <div class="lbl">{{ label() }}</div>`,
  styles: `
    :host{display:inline-flex;flex-direction:column;align-items:center;gap:6px}
    .ring{position:relative;width:var(--ring,120px);height:var(--ring,120px)}
    svg{width:100%;height:100%}
    .track{fill:none;stroke:var(--track);stroke-width:10}
    .bar{fill:none;stroke-width:10;stroke-linecap:round;transition:stroke-dashoffset 1.1s cubic-bezier(.2,.8,.2,1)}
    .num{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:1.6rem;font-weight:700;color:var(--text)}
    .num small{font-size:.65rem;font-weight:500;color:var(--muted);margin-left:2px;align-self:flex-end;margin-bottom:34px}
    .lbl{font-size:.8rem;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:.04em}`,
})
export class ScoreRingComponent {
  value = input.required<number>();
  label = input('');
  readonly C = 2 * Math.PI * 52;
  private shown = computed(() => Math.max(0, Math.min(100, this.value())));
  offset = computed(() => this.C * (1 - this.shown() / 100));
  color = computed(() => (this.shown() >= 75 ? 'var(--good)' : this.shown() >= 50 ? 'var(--warn)' : 'var(--bad)'));
}

@Component({
  selector: 'hl-verdict',
  template: `<span class="badge" [class]="cls()">{{ verdict() }}</span>`,
  styles: `.badge{display:inline-block;padding:3px 12px;border-radius:999px;font-size:.8rem;font-weight:700;white-space:nowrap}
    .strong{background:var(--good-bg);color:var(--good)}.possible{background:var(--warn-bg);color:var(--warn)}.not{background:var(--bad-bg);color:var(--bad)}`,
})
export class VerdictComponent {
  verdict = input.required<string>();
  cls = computed(() => (this.verdict() === 'Strong fit' ? 'badge strong' : this.verdict() === 'Possible fit' ? 'badge possible' : 'badge not'));
}
