import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { MissionFormValue } from './mission.model';

@Component({
  selector: 'app-mission-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './mission-form.component.html',
  styleUrl: './mission-form.component.scss'
})
export class MissionFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
  @Output() save = new EventEmitter<MissionFormValue>();
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
