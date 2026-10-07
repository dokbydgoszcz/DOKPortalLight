import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { CandidateFormValue, CandidateFormationEvent, CandidateRetreat, eventText, romanYear } from './candidate.model';

export type RetreatStatus = '' | 'pending' | 'done';

const YEARS = [1, 2, 3];

const emptyStatuses = (): Record<number, RetreatStatus> => ({ 1: '', 2: '', 3: '' });

@Component({
  selector: 'app-candidate-form',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './candidate-form.component.html',
  styleUrl: './candidate-form.component.scss'
})
export class CandidateFormComponent implements OnInit {
  @Input() open = false;
  @Input() editing = false;
  /** Historia zmian roku edytowanego kandydata (od najnowszej). */
  @Input() events: CandidateFormationEvent[] = [];
  @Input() set value(value: CandidateFormValue) {
    this._value = value;
    this.retreatStatus = emptyStatuses();
    for (const retreat of value.retreats) {
      this.retreatStatus[retreat.year] = retreat.isCompleted ? 'done' : 'pending';
    }
  }
  get value(): CandidateFormValue {
    return this._value;
  }
  @Output() save = new EventEmitter<CandidateFormValue>();
  @Output() cancel = new EventEmitter<void>();

  readonly years = YEARS;
  readonly romanYear = romanYear;
  readonly eventText = eventText;
  people: Person[] = [];
  retreatStatus: Record<number, RetreatStatus> = emptyStatuses();
  private _value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  /** Wymagana osoba, a przy zatrzymanej formacji także powód. */
  get canSave(): boolean {
    return !!this.value.personId && (!this.value.isFormationStopped || !!this.value.formationStopNote?.trim());
  }

  /** Ukończenie formacji oznacza ukończony III rok. */
  onCompletedChange(completed: boolean): void {
    this.value.isFormationCompleted = completed;
    if (completed) {
      this.value.year = 3;
    }
  }

  submit(): void {
    const retreats: CandidateRetreat[] = YEARS.filter(year => this.retreatStatus[year] !== '').map(year => ({
      year,
      isCompleted: this.retreatStatus[year] === 'done'
    }));
    const stopped = !!this.value.isFormationStopped;
    this.save.emit({
      ...this.value,
      retreats,
      isFormationStopped: this.value.isFormationStopped,
      formationStopNote: stopped ? this.value.formationStopNote : undefined
    });
  }
}
