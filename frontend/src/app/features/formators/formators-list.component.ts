import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormatorsService } from './formators.service';
import { Formator, FormatorFormValue } from './formator.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-formators-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './formators-list.component.html',
  styleUrl: './formators-list.component.scss'
})
export class FormatorsListComponent implements OnInit {
  readonly formators = signal<Formator[]>([]);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  people: Person[] = [];
  formValue: FormatorFormValue = { personId: '', function: '' };

  constructor(
    private readonly formatorsService: FormatorsService,
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe({
      next: result => (this.people = result.items),
      error: () => this.toast.error('Nie udało się wczytać listy osób.')
    });
  }

  load(): void {
    this.formatorsService.list().subscribe({
      next: formators => this.formators.set(formators),
      error: () => this.toast.error('Nie udało się wczytać listy formatorów.')
    });
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { personId: '', function: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(formator: Formator): void {
    this.editingId.set(formator.id);
    this.formValue = { personId: formator.personId, function: formator.function };
    this.isFormOpen.set(true);
  }

  onSave(): void {
    const id = this.editingId();
    const request$ = id ? this.formatorsService.update(id, this.formValue) : this.formatorsService.create(this.formValue);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano formatora.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać formatora.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deleteFormator(formator: Formator): void {
    if (!confirm(`Usunąć formatora „${formator.personFullName}”?`)) return;
    this.formatorsService.delete(formator.id).subscribe({
      next: () => {
        this.toast.success('Formator usunięty.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć formatora.')
    });
  }
}
