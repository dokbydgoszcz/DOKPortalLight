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
    this.formValue = { personId: '', function: '' };
    this.isFormOpen.set(true);
  }

  onSave(): void {
    this.formatorsService.create(this.formValue).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano formatora.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać formatora.')
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
