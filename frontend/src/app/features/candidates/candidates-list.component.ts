import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { CandidatesService } from './candidates.service';
import { Candidate, CandidateFormValue, CandidateFormationEvent, nextYearLabel, romanYear } from './candidate.model';
import { serverMessage } from '../../shared/http-error';
import { CandidateFormComponent } from './candidate-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, CandidateFormComponent, PaginationComponent, DatePipe],
  templateUrl: './candidates-list.component.html',
  styleUrl: './candidates-list.component.scss'
})
export class CandidatesListComponent implements OnInit {
  readonly candidates = signal<Candidate[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly romanYear = romanYear;
  readonly nextYearLabel = nextYearLabel;
  /** Historia roku edytowanego kandydata (pusta przy dodawaniu). */
  readonly formEvents = signal<CandidateFormationEvent[]>([]);
  /** Zaznaczeni do przeniesienia kandydaci. */
  readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  /** Zaznaczać można tylko tych, którzy są w trakcie formacji. */
  readonly selectable = computed(() => this.candidates().filter(c => c.status === 'InFormation'));
  readonly allSelected = computed(() => this.selectable().length > 0 && this.selectable().every(c => this.selectedIds().has(c.id)));
  formValue: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };

  readonly yearOneCount = signal(0);
  readonly yearTwoCount = signal(0);
  readonly yearThreeCount = signal(0);
  readonly missingOpinionsCount = signal(0);
  readonly completedCount = signal(0);

  constructor(
    private readonly candidatesService: CandidatesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadStats();
  }

  load(): void {
    this.candidatesService.search(undefined, this.page(), this.pageSize).subscribe({
      next: result => {
        this.candidates.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy kandydatów.')
    });
  }

  private loadStats(): void {
    this.candidatesService.search(undefined, 1, 1000).subscribe(result => {
      const all = result.items;
      const inFormation = all.filter(c => c.status === 'InFormation');
      this.yearOneCount.set(inFormation.filter(c => c.year === 1).length);
      this.yearTwoCount.set(inFormation.filter(c => c.year === 2).length);
      this.yearThreeCount.set(inFormation.filter(c => c.year === 3).length);
      this.missingOpinionsCount.set(inFormation.filter(c => c.opinionsCollected < c.opinionsRequired).length);
      this.completedCount.set(all.filter(c => c.status === 'Completed').length);
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formEvents.set([]);
    this.formValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };
    this.isFormOpen.set(true);
  }

  openEditForm(candidate: Candidate): void {
    this.editingId.set(candidate.id);
    this.formEvents.set(candidate.events);
    this.formValue = {
      personId: candidate.personId,
      year: candidate.year,
      attendancePercentage: candidate.attendancePercentage ?? undefined,
      opinionsCollected: candidate.opinionsCollected,
      retreats: candidate.retreats.map(r => ({ ...r })),
      isFormationCompleted: candidate.isFormationCompleted,
      isFormationStopped: candidate.isFormationStopped,
      formationStopNote: candidate.formationStopNote ?? undefined
    };
    this.isFormOpen.set(true);
  }

  onSave(value: CandidateFormValue): void {
    const id = this.editingId();
    const request$ = id ? this.candidatesService.update(id, value) : this.candidatesService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano kandydata.');
        this.load();
        this.loadStats();
      },
      error: err => this.toast.error(id ? serverMessage(err, 'Nie udało się zapisać zmian.') : 'Nie udało się dodać kandydata.')
    });
  }

  toggleSelected(candidate: Candidate): void {
    this.selectedIds.update(ids => {
      const next = new Set(ids);
      if (!next.delete(candidate.id)) next.add(candidate.id);
      return next;
    });
  }

  toggleAllSelected(): void {
    this.selectedIds.set(this.allSelected() ? new Set() : new Set(this.selectable().map(c => c.id)));
  }

  advanceOne(candidate: Candidate): void {
    const question = candidate.year >= 3
      ? `Zakończyć formację kandydata „${candidate.personFullName}”?`
      : `Przenieść „${candidate.personFullName}” do ${romanYear(candidate.year + 1)} roku?`;
    if (!confirm(question)) return;
    this.advance([candidate.id]);
  }

  advanceSelected(): void {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;
    if (!confirm(`Przenieść zaznaczonych kandydatów (${ids.length}) do następnego roku? Kandydaci z III roku zakończą formację.`)) return;
    this.advance(ids);
  }

  private advance(ids: string[]): void {
    this.candidatesService.advance(ids).subscribe({
      next: result => {
        if (result.advanced > 0) this.toast.success(`Przeniesiono do następnego roku: ${result.advanced}.`);
        if (result.completed > 0) this.toast.success(`Ukończyło formację: ${result.completed}.`);
        result.skipped.forEach(s => this.toast.error(`${s.personFullName ?? 'Kandydat'}: ${s.reason}`));
        this.selectedIds.set(new Set());
        this.load();
        this.loadStats();
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się przenieść kandydatów.'))
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deleteCandidate(candidate: Candidate): void {
    if (!confirm(`Usunąć kandydata „${candidate.personFullName}”?`)) return;
    this.candidatesService.delete(candidate.id).subscribe({
      next: () => {
        this.toast.success('Kandydat usunięty.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się usunąć kandydata.')
    });
  }
}
