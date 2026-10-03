import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SupervisionsService } from './supervisions.service';
import { CreateSupervisionValue, Institution, Supervision } from './supervision.model';
import { ToastService } from '../../core/notifications/toast.service';

export type SupervisionSort = 'dateDesc' | 'dateAsc' | 'institution';

@Component({
  selector: 'app-supervisions-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, FormsModule, DatePipe],
  templateUrl: './supervisions-list.component.html',
  styleUrl: './supervisions-list.component.scss'
})
export class SupervisionsListComponent implements OnInit {
  readonly supervisions = signal<Supervision[]>([]);
  readonly institutionFilter = signal<Institution | ''>('');
  readonly sortBy = signal<SupervisionSort>('dateDesc');
  /** Lista w wybranej kolejności: po dacie (od najnowszych lub najstarszych) albo po instytucji, a w niej po dacie malejąco. */
  readonly sortedSupervisions = computed(() => {
    const byDate = (a: Supervision, b: Supervision) => a.supervisionDate.localeCompare(b.supervisionDate);
    const items = [...this.supervisions()];
    switch (this.sortBy()) {
      case 'dateAsc':
        return items.sort(byDate);
      case 'institution':
        return items.sort((a, b) => a.institution.localeCompare(b.institution) || byDate(b, a));
      default:
        return items.sort((a, b) => byDate(b, a));
    }
  });
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newSupervision: CreateSupervisionValue = { institution: 'DOK', groupLabel: '', supervisionDate: '' };

  constructor(
    private readonly supervisionsService: SupervisionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  onInstitutionFilterChange(value: Institution | ''): void {
    this.institutionFilter.set(value);
    this.load();
  }

  onSortChange(value: SupervisionSort): void {
    this.sortBy.set(value);
  }

  load(): void {
    const institution = this.institutionFilter();
    this.supervisionsService.list(institution || undefined).subscribe({
      next: supervisions => this.supervisions.set(supervisions),
      error: () => this.toast.error('Nie udało się wczytać listy superwizji.')
    });
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newSupervision = { institution: 'DOK', groupLabel: '', supervisionDate: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(supervision: Supervision): void {
    this.editingId.set(supervision.id);
    this.newSupervision = {
      institution: supervision.institution,
      groupLabel: supervision.groupLabel,
      supervisionDate: supervision.supervisionDate,
      attendeesCount: supervision.attendeesCount ?? undefined,
      expectedCount: supervision.expectedCount ?? undefined,
      topic: supervision.topic ?? undefined,
      conclusion: supervision.conclusion ?? undefined
    };
    this.isFormOpen.set(true);
  }

  createSupervision(): void {
    const id = this.editingId();
    const request$ = id
      ? this.supervisionsService.update(id, this.newSupervision)
      : this.supervisionsService.create(this.newSupervision);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano superwizję.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać superwizji.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteSupervision(supervision: Supervision): void {
    if (!confirm(`Usunąć superwizję „${supervision.groupLabel}”?`)) return;
    this.supervisionsService.delete(supervision.id).subscribe({
      next: () => {
        this.toast.success('Superwizja usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć superwizji.')
    });
  }
}
