import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SupervisionsService } from './supervisions.service';
import { CreateSupervisionValue, Supervision } from './supervision.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-supervisions-list',
  standalone: true,
  imports: [ExportButtonComponent, FormsModule],
  templateUrl: './supervisions-list.component.html',
  styleUrl: './supervisions-list.component.scss'
})
export class SupervisionsListComponent implements OnInit {
  readonly supervisions = signal<Supervision[]>([]);
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

  load(): void {
    this.supervisionsService.list().subscribe({
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
