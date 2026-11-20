import { Component } from '@angular/core';
import { RibbonShape, ribbonPath } from './ribbon-path';

/** Kilka cienkich, półprzezroczystych wstążek płynących powoli przez zielony panel logowania. */
const RIBBONS: ReadonlyArray<{ shape: RibbonShape; tone: 'gold' | 'light'; duration: number; opacity: number }> = [
  { shape: { center: 440, amplitude: 60, thickness: 34, twist: 26, phase: 0 }, tone: 'gold', duration: 46, opacity: 0.22 },
  { shape: { center: 470, amplitude: 75, thickness: 18, twist: 14, phase: 1.4 }, tone: 'light', duration: 64, opacity: 0.14 },
  { shape: { center: 500, amplitude: 45, thickness: 10, twist: 8, phase: 2.6 }, tone: 'gold', duration: 38, opacity: 0.18 }
];

@Component({
  selector: 'app-login-ribbons',
  standalone: true,
  template: `
    <svg class="ribbons" viewBox="0 0 1200 600" preserveAspectRatio="none" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id="ribbon-gold" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stop-color="#f3d58a" stop-opacity="0" />
          <stop offset=".35" stop-color="#e8c060" />
          <stop offset=".65" stop-color="#fbe9b8" />
          <stop offset="1" stop-color="#f3d58a" stop-opacity="0" />
        </linearGradient>
        <linearGradient id="ribbon-light" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stop-color="#f6f5f1" stop-opacity="0" />
          <stop offset=".5" stop-color="#f6f5f1" />
          <stop offset="1" stop-color="#f6f5f1" stop-opacity="0" />
        </linearGradient>
        <!-- Wstążki wygasają przy brzegach panelu, zamiast się o nie ucinać. -->
        <linearGradient id="ribbon-fade" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stop-color="#fff" stop-opacity="0" />
          <stop offset=".18" stop-color="#fff" />
          <stop offset=".82" stop-color="#fff" />
          <stop offset="1" stop-color="#fff" stop-opacity="0" />
        </linearGradient>
        <mask id="ribbon-mask" maskUnits="userSpaceOnUse" x="0" y="0" width="1200" height="600">
          <rect width="1200" height="600" fill="url(#ribbon-fade)" />
        </mask>
      </defs>
      <g mask="url(#ribbon-mask)">
        @for (r of ribbons; track $index) {
          <path class="ribbon" [attr.d]="r.d" [attr.fill]="'url(#ribbon-' + r.tone + ')'" [style.opacity]="r.opacity"
            [style.animation-duration.s]="r.duration" />
        }
      </g>
    </svg>
  `,
  styles: [`
    :host { position: absolute; inset: 0; pointer-events: none; overflow: hidden; }
    .ribbons {
      position: absolute; left: -10%; top: 0; width: 120%; height: 100%;
      transform: rotate(-7deg); transform-origin: 50% 80%;
      animation: ribbons-in 1.6s ease-out both;
    }
    .ribbon {
      /* Ścieżka ma dwa okresy fali – przesunięcie o połowę (jeden okres) zamyka pętlę bez szwu. */
      transform-box: fill-box;
      animation: ribbon-flow linear infinite;
      mix-blend-mode: screen;
    }
    @keyframes ribbon-flow { from { transform: translateX(0); } to { transform: translateX(-50%); } }
    @keyframes ribbons-in { from { opacity: 0; } to { opacity: 1; } }
    @media (prefers-reduced-motion: reduce) {
      .ribbon, .ribbons { animation: none; }
    }
  `]
})
export class LoginRibbonsComponent {
  readonly ribbons = RIBBONS.map(r => ({ ...r, d: ribbonPath(r.shape) }));
}
