import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { CandidateFormValue } from './candidate.model';

@Component({
  selector: 'app-candidate-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './candidate-form.component.html',
  styleUrl: './candidate-form.component.scss'
})
export class CandidateFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };
  @Output() save = new EventEmitter<CandidateFormValue>();
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
