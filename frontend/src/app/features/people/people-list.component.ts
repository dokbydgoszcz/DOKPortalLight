import { Component, OnInit, signal } from '@angular/core';
import { PeopleService } from './people.service';
import { Person, PersonFormValue } from './person.model';
import { PersonFormComponent } from './person-form.component';

@Component({
  selector: 'app-people-list',
  standalone: true,
  imports: [PersonFormComponent],
  templateUrl: './people-list.component.html',
  styleUrl: './people-list.component.scss'
})
export class PeopleListComponent implements OnInit {
  readonly people = signal<Person[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: PersonFormValue = { firstName: '', lastName: '' };

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.peopleService.search(this.query()).subscribe(result => this.people.set(result.items));
  }

  onSearch(value: string): void {
    this.query.set(value);
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
      notes: person.notes ?? undefined
    };
    this.isFormOpen.set(true);
  }

  onSave(value: PersonFormValue): void {
    const id = this.editingId();
    const request$ = id ? this.peopleService.update(id, value) : this.peopleService.create(value);
    request$.subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }
}
