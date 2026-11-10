import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { HELP_AUDIENCES, HELP_TOPICS, HelpAudience } from './help-content';

/** Krótka instrukcja „co gdzie kliknąć” – osobno dla katechisty, dyrektora i spraw konta. */
@Component({
  selector: 'app-help',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-heading">
      <div><h2>Pomoc</h2><p>Krótka instrukcja: co gdzie kliknąć. Wybierz, kim jesteś w portalu.</p></div>
      <button class="btn ghost no-print" type="button" (click)="print()">Drukuj</button>
    </div>

    <div class="card">
      <div class="tabs no-print" role="tablist">
        @for (a of audiences; track a.id) {
          <button class="tab" type="button" role="tab" [attr.aria-selected]="audience() === a.id" [class.active]="audience() === a.id" (click)="audience.set(a.id)">{{ a.label }}</button>
        }
      </div>
      <div class="card-body">
        <p class="intro">{{ current().intro }}</p>
        @for (topic of topics(); track topic.title) {
          <section class="topic">
            <div class="topic-head">
              <h3>{{ topic.title }}</h3>
              @if (topic.link) { <a class="link no-print" [routerLink]="topic.link">Otwórz →</a> }
            </div>
            <div class="where">{{ topic.where }}</div>
            <ol>
              @for (step of topic.steps; track step) { <li>{{ step }}</li> }
            </ol>
            @if (topic.tip) { <div class="note">{{ topic.tip }}</div> }
          </section>
        }
        <p class="small-muted">Czegoś brakuje albo coś nie działa tak, jak tu opisano? Napisz do administratora portalu.</p>
      </div>
    </div>
  `,
  styles: [`
    .intro { margin: 0 0 14px; font-size: 14px; }
    .topic { padding: 14px 0; border-top: 1px solid var(--line, #dfe5e2); }
    .topic-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; }
    .topic h3 { margin: 0; font-size: 15px; }
    .topic-head .link { font-size: 12px; white-space: nowrap; }
    .where { font-size: 11px; font-weight: 800; text-transform: uppercase; color: #6C7A75; margin: 4px 0 8px; }
    ol { margin: 0; padding-left: 20px; display: grid; gap: 6px; font-size: 13px; line-height: 1.5; max-width: 760px; }
    .note { margin-top: 10px; max-width: 760px; }
    @media print { .no-print { display: none !important; } .topic { break-inside: avoid; } }
  `]
})
export class HelpComponent {
  private readonly auth = inject(AuthService);
  readonly audiences = HELP_AUDIENCES;

  /** Na start zakładka pasująca do uprawnień: kto zarządza sprawami, kandydatami albo misjami, ten widzi część dla dyrektora. */
  readonly audience = signal<HelpAudience>(
    this.auth.hasAnyPermission([Permissions.DokCasesManage, Permissions.CandidatesManage, Permissions.MissionsManage]) ? 'director' : 'catechist');

  current() {
    return this.audiences.find(a => a.id === this.audience())!;
  }

  topics() {
    return HELP_TOPICS[this.audience()];
  }

  print(): void {
    window.print();
  }
}
