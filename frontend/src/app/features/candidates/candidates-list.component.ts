import { Component, OnInit, computed, signal } from '@angular/core';
import { CandidatesService } from './candidates.service';
import { Candidate, CandidateFormValue } from './candidate.model';
import { CandidateFormComponent } from './candidate-form.component';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [CandidateFormComponent],
  templateUrl: './candidates-list.component.html',
  styleUrl: './candidates-list.component.scss'
})
export class CandidatesListComponent implements OnInit {
  readonly candidates = signal<Candidate[]>([]);
  readonly isFormOpen = signal(false);
  formValue: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };

  readonly yearOneCount = computed(() => this.candidates().filter(c => c.year === 1).length);
  readonly yearTwoCount = computed(() => this.candidates().filter(c => c.year === 2).length);
  readonly yearThreeCount = computed(() => this.candidates().filter(c => c.year === 3).length);
  readonly missingOpinionsCount = computed(() => this.candidates().filter(c => c.opinionsCollected < c.opinionsRequired).length);

  constructor(
    private readonly candidatesService: CandidatesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.candidatesService.search().subscribe({
      next: result => this.candidates.set(result.items),
      error: () => this.toast.error('Nie udało się wczytać listy kandydatów.')
    });
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
      },
      error: () => this.toast.error('Nie udało się usunąć kandydata.')
    });
  }
}
