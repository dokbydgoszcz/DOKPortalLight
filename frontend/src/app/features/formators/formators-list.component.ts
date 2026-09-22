import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormatorsService } from './formators.service';
import { Formator, FormatorFormValue } from './formator.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';

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
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.formatorsService.list().subscribe(formators => this.formators.set(formators));
  }

  openAddForm(): void {
    this.formValue = { personId: '', function: '' };
    this.isFormOpen.set(true);
  }

  onSave(): void {
    this.formatorsService.create(this.formValue).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }
}
