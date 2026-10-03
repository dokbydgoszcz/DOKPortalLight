import { Component } from '@angular/core';
import { APP_NAME, APP_VERSION, DIOCESE_NAME, ORGANIZATION_NAME } from '../core/app-info';

/** Dolny pasek: prawa autorskie z nazwą diecezji oraz nazwa i wersja aplikacji. */
@Component({
  selector: 'app-footer',
  standalone: true,
  template: `
    <footer class="app-footer">
      <span>© {{ year }} {{ diocese }} · {{ organization }}</span>
      <span>{{ appName }} · wersja {{ version }}</span>
    </footer>
  `,
  styles: `
    .app-footer {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 4px 20px;
      padding: 14px 28px;
      border-top: 1px solid var(--line);
      background: white;
      color: var(--muted);
      font-size: 12px;
    }
    @media (max-width: 900px) {
      .app-footer { padding: 12px 14px; }
    }
  `
})
export class AppFooterComponent {
  readonly year = new Date().getFullYear();
  readonly diocese = DIOCESE_NAME;
  readonly organization = ORGANIZATION_NAME;
  readonly appName = APP_NAME;
  readonly version = APP_VERSION;
}
