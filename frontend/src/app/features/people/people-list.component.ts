import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { PeopleService } from './people.service';
import { FUNCTION_LABELS, FUNCTION_TYPES, FunctionType, Person, PersonFormValue, functionText } from './person.model';
import { ParishesService } from '../parish-board/parishes.service';
import { Parish } from '../parish-board/parish-need.model';
import { PersonFormComponent } from './person-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-people-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, PersonFormComponent, PaginationComponent],
  templateUrl: './people-list.component.html',
  styleUrl: './people-list.component.scss'
})
export class PeopleListComponent implements OnInit {
  readonly people = signal<Person[]>([]);
  readonly query = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly functionFilter = signal<FunctionType | ''>('');
  readonly parishes = signal<Parish[]>([]);
  readonly functionTypes = FUNCTION_TYPES;
  readonly functionLabels = FUNCTION_LABELS;
  readonly functionText = functionText;
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: PersonFormValue = { firstName: '', lastName: '' };

  constructor(
    private readonly peopleService: PeopleService,
    private readonly parishesService: ParishesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.parishesService.list().subscribe({ next: parishes => this.parishes.set(parishes), error: () => {} });
  }

  load(): void {
    this.peopleService.search(this.query(), this.page(), this.pageSize, this.functionFilter() || undefined).subscribe({
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

  onFunctionFilter(value: FunctionType | ''): void {
    this.functionFilter.set(value);
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
      birthDate: person.birthDate ?? undefined,
      parishId: person.parishId ?? undefined,
      notes: person.notes ?? undefined,
      nameDayMonth: person.nameDayMonth ?? undefined,
      nameDayDay: person.nameDayDay ?? undefined,
      functions: person.functions.map(f => ({
        type: f.type,
        parishId: f.parishId ?? undefined,
        institutedOn: f.institutedOn ?? undefined,
        notes: f.notes ?? undefined
      }))
    };
    this.isFormOpen.set(true);
  }

  onSave(value: PersonFormValue, confirmDuplicate = false): void {
    const id = this.editingId();
    const payload = confirmDuplicate ? { ...value, confirmDuplicate: true } : value;
    const request$ = id ? this.peopleService.update(id, payload) : this.peopleService.create(payload);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano osobę.');
        this.load();
      },
      error: (error: HttpErrorResponse) => this.handleSaveError(error, value)
    });
  }

  /** E-mail zajęty: komunikat serwera. Telefon już używany: pytamy, czy zapisać mimo to. */
  private handleSaveError(error: HttpErrorResponse, value: PersonFormValue): void {
    const problem = error.status === 409 ? error.error : null;
    if (problem?.code === 'EmailTaken') {
      this.toast.error(problem.title);
    } else if (problem?.code === 'PhoneDuplicate') {
      if (confirm(`${problem.title} Zapisać mimo to?`)) {
        this.onSave(value, true);
      }
    } else {
      this.toast.error('Nie udało się zapisać osoby.');
    }
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
