import { Component, OnInit, signal } from '@angular/core';
import { CandidatesService } from './candidates.service';
import { Candidate, CandidateFormValue } from './candidate.model';
import { CandidateFormComponent } from './candidate-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [CandidateFormComponent, PaginationComponent],
  templateUrl: './candidates-list.component.html',
  styleUrl: './candidates-list.component.scss'
})
export class CandidatesListComponent implements OnInit {
  readonly candidates = signal<Candidate[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  formValue: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };

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
    this.formValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };
    this.isFormOpen.set(true);
  }

  onSave(value: CandidateFormValue): void {
    this.candidatesService.create(value).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano kandydata.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się dodać kandydata.')
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
