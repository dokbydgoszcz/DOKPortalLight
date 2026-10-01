import { Component, OnInit, signal } from '@angular/core';
import { PeopleService } from './people.service';
import { Person, PersonFormValue } from './person.model';
import { PersonFormComponent } from './person-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-people-list',
  standalone: true,
  imports: [PersonFormComponent, PaginationComponent],
  templateUrl: './people-list.component.html',
  styleUrl: './people-list.component.scss'
})
export class PeopleListComponent implements OnInit {
  readonly people = signal<Person[]>([]);
  readonly query = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: PersonFormValue = { firstName: '', lastName: '' };

  constructor(
    private readonly peopleService: PeopleService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.peopleService.search(this.query(), this.page(), this.pageSize).subscribe({
      next: result => {
        this.people.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy osób.')
    });
  }

  onSearch(value: string): void {
    this.query.set(value);
    this.page.set(1);
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { firstName: '', lastName: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(person: Person): void {
    this.editingId.set(person.id);
    this.formValue = {
      firstName: person.firstName,
      lastName: person.lastName,
      email: person.email ?? undefined,
      phone: person.phone ?? undefined,
      notes: person.notes ?? undefined,
      nameDayMonth: person.nameDayMonth ?? undefined,
      nameDayDay: person.nameDayDay ?? undefined
    };
    this.isFormOpen.set(true);
  }

  onSave(value: PersonFormValue): void {
    const id = this.editingId();
    const request$ = id ? this.peopleService.update(id, value) : this.peopleService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano osobę.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się zapisać osoby.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deletePerson(person: Person): void {
    if (!confirm(`Usunąć osobę „${person.fullName}”?`)) return;
    this.peopleService.delete(person.id).subscribe({
      next: () => {
        this.toast.success('Osoba usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć osoby.')
    });
  }
}
