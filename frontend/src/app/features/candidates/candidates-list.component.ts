import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { CandidatesService } from './candidates.service';
import { Candidate, CandidateFormValue, romanYear } from './candidate.model';
import { serverMessage } from '../../shared/http-error';
import { CandidateFormComponent } from './candidate-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, CandidateFormComponent, PaginationComponent],
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
  formValue: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };

  readonly yearOneCount = signal(0);
  readonly yearTwoCount = signal(0);
  readonly yearThreeCount = signal(0);
  readonly missingOpinionsCount = signal(0);

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
      this.yearOneCount.set(all.filter(c => c.year === 1).length);
      this.yearTwoCount.set(all.filter(c => c.year === 2).length);
      this.yearThreeCount.set(all.filter(c => c.year === 3).length);
      this.missingOpinionsCount.set(all.filter(c => c.opinionsCollected < c.opinionsRequired).length);
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };
    this.isFormOpen.set(true);
  }

  openEditForm(candidate: Candidate): void {
    this.editingId.set(candidate.id);
    this.formValue = {
      personId: candidate.personId,
      year: candidate.year,
      attendancePercentage: candidate.attendancePercentage ?? undefined,
      opinionsCollected: candidate.opinionsCollected,
      retreats: candidate.retreats.map(r => ({ ...r }))
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
