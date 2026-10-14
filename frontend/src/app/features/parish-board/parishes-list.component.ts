import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishFormValue, ParishesService } from './parishes.service';
import { RouterLink } from '@angular/router';
import { PeopleService } from '../people/people.service';
import { Parish } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

@Component({
  selector: 'app-parishes-list',
  standalone: true,
  imports: [ExportButtonComponent, HasPermissionDirective, FormsModule, RouterLink],
  templateUrl: './parishes-list.component.html',
  styleUrl: './parishes-list.component.scss'
})
export class ParishesListComponent implements OnInit {
  readonly parishes = signal<Parish[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newParish: ParishFormValue = { name: '', city: '', pastorPersonId: null };
  /** Osoby z funkcją Proboszcz – do wyboru w formularzu. */
  readonly pastors = signal<{ id: string; fullName: string; parishName: string | null }[]>([]);

  constructor(
    private readonly parishesService: ParishesService,
    private readonly toast: ToastService,
    private readonly peopleService: PeopleService
  ) {}

  private loadPastors(): void {
    this.peopleService.search('', 1, 200, 'Pastor').subscribe({
      next: result => this.pastors.set(result.items.map(p => ({
        id: p.id,
        fullName: p.fullName,
        parishName: p.functions?.find(f => f.type === 'Pastor')?.parishName ?? null
      }))),
      error: () => this.pastors.set([])
    });
  }

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
    this.newParish = { name: '', city: '', pastorPersonId: null };
    this.isFormOpen.set(true);
    this.loadPastors();
  }

  openEditForm(parish: Parish): void {
    this.editingId.set(parish.id);
    this.newParish = { name: parish.name, city: parish.city ?? '', pastorPersonId: parish.pastorPersonId ?? null };
    this.isFormOpen.set(true);
    this.loadPastors();
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
      error: err => this.toast.error(serverMessage(err, id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać parafii.'))
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
      error: err => this.toast.error(serverMessage(err, 'Nie udało się usunąć parafii.'))
    });
  }
}
