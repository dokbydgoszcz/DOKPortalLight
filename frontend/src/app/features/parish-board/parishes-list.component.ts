import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishesService } from './parishes.service';
import { Parish } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-parishes-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './parishes-list.component.html',
  styleUrl: './parishes-list.component.scss'
})
export class ParishesListComponent implements OnInit {
  readonly parishes = signal<Parish[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
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
    this.newParish = { name: '', city: '' };
    this.isFormOpen.set(true);
  }

  createParish(): void {
    if (!this.newParish.name.trim()) {
      this.toast.error('Podaj nazwę parafii.');
      return;
    }
    this.parishesService.create(this.newParish).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano parafię.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać parafii.')
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
