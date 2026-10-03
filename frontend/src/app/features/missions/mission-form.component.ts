import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { ParishesService } from '../parish-board/parishes.service';
import { Parish } from '../parish-board/parish-need.model';
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
  @Input() editing = false;
  @Input() value: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
  @Output() save = new EventEmitter<MissionFormValue>();
  @Output() cancel = new EventEmitter<void>();

  people: Person[] = [];
  /** Parafie z rejestru – podpowiedzi dla pola miejsca posługi (pole nadal przyjmuje dowolny tekst). */
  parishes: Parish[] = [];

  constructor(
    private readonly peopleService: PeopleService,
    private readonly parishesService: ParishesService
  ) {}

  ngOnInit(): void {
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
    // Podpowiedzi to tylko wygoda – gdy lista się nie wczyta, pole działa jak zwykły tekst.
    this.parishesService.list().subscribe({ next: parishes => (this.parishes = parishes), error: () => {} });
  }

  submit(): void {
    // Wyczyszczone pole daty daje pusty tekst, którego API nie przyjmie jako daty – opcjonalną datę pomijamy.
    this.save.emit({ ...this.value, grantedDate: this.value.grantedDate || undefined });
  }
}
