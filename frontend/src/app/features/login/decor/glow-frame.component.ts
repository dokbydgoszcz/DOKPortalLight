import { Component } from '@angular/core';

/**
 * Ramka z delikatnym światłem, które powoli okrąża zawartość (jak na dawnych ekranach startowych Office).
 * Zawartość musi mieć własne tło i zaokrąglenie – ramka rysuje się tuż pod jej krawędzią.
 */
@Component({
  selector: 'app-glow-frame',
  standalone: true,
  template: `<span class="halo" aria-hidden="true"></span><span class="rim" aria-hidden="true"></span><ng-content />`,
  styles: [`
    :host { position: relative; display: block; border-radius: 22px; isolation: isolate; }
    :host ::ng-deep > :not(.halo):not(.rim) { position: relative; z-index: 1; }
    .rim, .halo {
      position: absolute; inset: -1.5px; border-radius: 23.5px; z-index: 0; pointer-events: none;
      /* Krótki łuk światła na neutralnym tle; reszta obwodu prawie niewidoczna. */
      background: conic-gradient(from var(--dok-glow-angle, 0deg),
        rgba(200, 155, 45, .10) 0deg,
        rgba(200, 155, 45, .10) 250deg,
        rgba(232, 192, 96, .55) 300deg,
        rgba(255, 246, 220, .95) 330deg,
        rgba(232, 192, 96, .55) 345deg,
        rgba(200, 155, 45, .10) 360deg);
      animation: glow-turn 9s linear infinite;
    }
    .halo { inset: -6px; border-radius: 28px; filter: blur(14px); opacity: .35; }
    :host { animation: frame-in .9s cubic-bezier(.2, .7, .2, 1) both; }
    @keyframes glow-turn { to { --dok-glow-angle: 360deg; } }
    @keyframes frame-in { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) {
      :host, .rim, .halo { animation: none; }
    }
    @media (max-width: 720px) {
      .rim, .halo { display: none; }
      :host { border-radius: 0; }
    }
  `]
})
export class GlowFrameComponent {}
