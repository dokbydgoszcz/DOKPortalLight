import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PersonPickerComponent } from '../../shared/person-picker/person-picker.component';
import { DokCaseFormValue, DokPath, DokStage } from './dok-case.model';
import { DOK_PATH_LABELS, DOK_STAGE_LABELS, firstStageOf, stagesOf } from './dok-stages';

@Component({
  selector: 'app-dok-case-form',
  standalone: true,
  imports: [FormsModule, PersonPickerComponent],
  templateUrl: './dok-case-form.component.html',
  styleUrl: './dok-case-form.component.scss'
})
export class DokCaseFormComponent {
  @Input() open = false;
  @Input() value: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Prekatechumenate', catechistPersonId: '' };
  /** Imię i nazwisko podopiecznego edytowanej sprawy; pusty = nowa sprawa. Przy edycji podopiecznego nie można zmienić. */
  @Input() editingName: string | null = null;
  @Output() save = new EventEmitter<DokCaseFormValue>();
  @Output() cancel = new EventEmitter<void>();

  readonly pathLabels = DOK_PATH_LABELS;
  readonly stageLabels = DOK_STAGE_LABELS;
  readonly paths = Object.keys(DOK_PATH_LABELS) as DokPath[];
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

  /** Pusty mentor nie jest wysyłany (serwer traktuje brak jako „bez mentora”). */
  submit(): void {
    const { mentorPersonId, ...rest } = this.value;
    this.save.emit(mentorPersonId ? { ...rest, mentorPersonId } : rest);
  }
}
