import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { DokCaseFormValue, DokPath, DokStage } from './dok-case.model';
import { DOK_PATH_LABELS, DOK_STAGE_LABELS, firstStageOf, stagesOf } from './dok-stages';

@Component({
  selector: 'app-dok-case-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './dok-case-form.component.html',
  styleUrl: './dok-case-form.component.scss'
})
export class DokCaseFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Prekatechumenate', catechistPersonId: '' };
  @Output() save = new EventEmitter<DokCaseFormValue>();
  @Output() cancel = new EventEmitter<void>();

  readonly pathLabels = DOK_PATH_LABELS;
  readonly stageLabels = DOK_STAGE_LABELS;
  readonly paths = Object.keys(DOK_PATH_LABELS) as DokPath[];
  people: Person[] = [];

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  /** Etapy dostępne na wybranej ścieżce. */
  get stages(): readonly DokStage[] {
    return stagesOf(this.value.path);
  }

  /** Po zmianie ścieżki etap, którego na niej nie ma, wraca na pierwszy etap tej ścieżki. */
  onPathChange(path: DokPath): void {
    this.value.path = path;
    if (!stagesOf(path).includes(this.value.stage)) {
      this.value.stage = firstStageOf(path);
    }
  }

  submit(): void {
    this.save.emit(this.value);
  }
}
