import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { DokCaseFormValue } from './dok-case.model';

@Component({
  selector: 'app-dok-case-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './dok-case-form.component.html',
  styleUrl: './dok-case-form.component.scss'
})
export class DokCaseFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
  @Output() save = new EventEmitter<DokCaseFormValue>();
  @Output() cancel = new EventEmitter<void>();

  people: Person[] = [];

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  submit(): void {
    this.save.emit(this.value);
  }
}
