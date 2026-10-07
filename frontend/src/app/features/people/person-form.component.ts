import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Parish } from '../parish-board/parish-need.model';
import { FUNCTION_LABELS, FUNCTION_TYPES, FunctionType, PersonFormValue, PersonFunctionInput } from './person.model';

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
  @Input() parishes: Parish[] = [];
  @Output() save = new EventEmitter<PersonFormValue>();
  @Output() cancel = new EventEmitter<void>();

  readonly functionTypes = FUNCTION_TYPES;
  readonly functionLabels = FUNCTION_LABELS;

  addFunction(): void {
    const used = new Set((this.value.functions ?? []).map(f => f.type));
    const type: FunctionType = FUNCTION_TYPES.find(t => !used.has(t)) ?? FUNCTION_TYPES[0];
    this.value.functions = [...(this.value.functions ?? []), { type }];
  }

  removeFunction(index: number): void {
    this.value.functions = (this.value.functions ?? []).filter((_, i) => i !== index);
  }

  submit(): void {
    this.save.emit(this.value.functions ? { ...this.value, functions: this.value.functions.map(clean) } : this.value);
  }
}

/** Puste daty i notatki nie jadą do API; parafia tylko u proboszcza (po zmianie funkcji na inną znika). */
function clean(fn: PersonFunctionInput): PersonFunctionInput {
  const result: PersonFunctionInput = { type: fn.type };
  if (fn.type === 'Pastor' && fn.parishId) result.parishId = fn.parishId;
  if (fn.institutedOn) result.institutedOn = fn.institutedOn;
  if (fn.notes?.trim()) result.notes = fn.notes.trim();
  return result;
}
