import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PersonFormValue } from './person.model';

@Component({
  selector: 'app-person-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './person-form.component.html',
  styleUrl: './person-form.component.scss'
})
export class PersonFormComponent {
  @Input() open = false;
  @Input() value: PersonFormValue = { firstName: '', lastName: '' };
  @Output() save = new EventEmitter<PersonFormValue>();
  @Output() cancel = new EventEmitter<void>();

  submit(): void {
    this.save.emit(this.value);
  }
}
