import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishesService } from './parishes.service';
import { Parish } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-parishes-list',
  standalone: true,
  imports: [ExportButtonComponent, HasPermissionDirective, FormsModule],
  templateUrl: './parishes-list.component.html',
  styleUrl: './parishes-list.component.scss'
})
export class ParishesListComponent implements OnInit {
  readonly parishes = signal<Parish[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newParish: { name: string; city?: string } = { name: '', city: '' };

  constructor(
    private readonly parishesService: ParishesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  readonly filteredParishes = () => {
    const term = this.query().trim().toLowerCase();
    if (!term) return this.parishes();
    return this.parishes().filter(
      p => p.name.toLowerCase().includes(term) || (p.city ?? '').toLowerCase().includes(term)
    );
  };

  load(): void {
    this.parishesService.list().subscribe({
      next: parishes => this.parishes.set(parishes),
      error: () => this.toast.error('Nie udało się wczytać listy parafii.')
    });
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newParish = { name: '', city: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(parish: Parish): void {
    this.editingId.set(parish.id);
    this.newParish = { name: parish.name, city: parish.city ?? '' };
    this.isFormOpen.set(true);
  }

  saveParish(): void {
    if (!this.newParish.name.trim()) {
      this.toast.error('Podaj nazwę parafii.');
      return;
    }
    const id = this.editingId();
    const request$ = id ? this.parishesService.update(id, this.newParish) : this.parishesService.create(this.newParish);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano parafię.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać parafii.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteParish(parish: Parish): void {
    if (!confirm(`Usunąć parafię „${parish.name}” (${parish.city})?`)) return;
    this.parishesService.delete(parish.id).subscribe({
      next: () => {
        this.toast.success('Parafia usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć parafii.')
    });
  }
}
