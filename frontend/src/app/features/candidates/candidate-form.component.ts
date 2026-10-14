import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { PersonPickerComponent } from '../../shared/person-picker/person-picker.component';
import { CandidateFormValue, CandidateFormationEvent, CandidateRetreat, completionGaps, eventText, romanYear } from './candidate.model';

/** Frekwencja z list obecności zajęć SKŚP (gdy kandydat ma zaznaczoną obecność na zajęciach). */
export interface ClassAttendance {
  percentage: number;
  attended: number;
  recorded: number;
}

export type RetreatStatus = '' | 'pending' | 'done';

const YEARS = [1, 2, 3];

const emptyStatuses = (): Record<number, RetreatStatus> => ({ 1: '', 2: '', 3: '' });

@Component({
  selector: 'app-candidate-form',
  standalone: true,
  imports: [FormsModule, DatePipe, PersonPickerComponent],
  templateUrl: './candidate-form.component.html',
  styleUrl: './candidate-form.component.scss'
})
export class CandidateFormComponent {
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
  /** Kandydat już ukończył formację (wtedy nie sprawdzamy warunków ponownie). */
  @Input() wasCompleted = false;
  @Input() classAttendance: ClassAttendance | null = null;
  @Output() save = new EventEmitter<CandidateFormValue>();
  @Output() cancel = new EventEmitter<void>();

  readonly years = YEARS;
  readonly romanYear = romanYear;
  readonly eventText = eventText;
  retreatStatus: Record<number, RetreatStatus> = emptyStatuses();
  private _value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };

  private retreatsFromStatus(): CandidateRetreat[] {
    return YEARS.filter(year => this.retreatStatus[year] !== '').map(year => ({ year, isCompleted: this.retreatStatus[year] === 'done' }));
  }

  /** Braki do ukończenia formacji – pokazywane, gdy zaznaczasz „Ukończył formację”. */
  get gaps(): string[] {
    if (!this.value.isFormationCompleted || this.wasCompleted) return [];
    const attendance = this.classAttendance ? this.classAttendance.percentage : this.value.attendancePercentage;
    return completionGaps(this.retreatsFromStatus(), Number(this.value.opinionsCollected) || 0, 2, attendance);
  }

  /** Wymagana osoba, przy zatrzymanej formacji powód, a przy ukończeniu z brakami – potwierdzenie. */
  get canSave(): boolean {
    return !!this.value.personId
      && (!this.value.isFormationStopped || !!this.value.formationStopNote?.trim())
      && (this.gaps.length === 0 || !!this.value.confirmIncomplete);
  }

  /** Ukończenie formacji oznacza ukończony III rok. */
  onCompletedChange(completed: boolean): void {
    this.value.isFormationCompleted = completed;
    if (completed) {
      this.value.year = 3;
    }
  }

  submit(): void {
    const retreats = this.retreatsFromStatus();
    const stopped = !!this.value.isFormationStopped;
    const { confirmIncomplete, ...rest } = this.value;
    this.save.emit({
      ...rest,
      retreats,
      isFormationStopped: this.value.isFormationStopped,
      formationStopNote: stopped ? this.value.formationStopNote : undefined,
      ...(this.gaps.length > 0 && confirmIncomplete ? { confirmIncomplete: true } : {})
    });
  }
}
