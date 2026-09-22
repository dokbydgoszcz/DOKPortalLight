import { Component, OnInit, computed, signal } from '@angular/core';
import { DokCasesService } from './dok-cases.service';
import { DokCase, DokCaseFormValue } from './dok-case.model';
import { DokCaseFormComponent } from './dok-case-form.component';

const PATH_LABELS: Record<string, string> = {
  BaptismCandidate: 'Chrzest',
  Confirmation: 'Bierzmowanie',
  Communion: 'Stół Pański',
  Conversion: 'Konwersja',
  ReturnToUnity: 'Powrót do Jedności'
};

@Component({
  selector: 'app-dok-cases-list',
  standalone: true,
  imports: [DokCaseFormComponent],
  templateUrl: './dok-cases-list.component.html',
  styleUrl: './dok-cases-list.component.scss'
})
export class DokCasesListComponent implements OnInit {
  readonly cases = signal<DokCase[]>([]);
  readonly isFormOpen = signal(false);
  formValue: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };

  readonly baptismCount = computed(() => this.cases().filter(c => c.path === 'BaptismCandidate').length);
  readonly confirmationCount = computed(() => this.cases().filter(c => c.path === 'Confirmation').length);
  readonly conversionCount = computed(() => this.cases().filter(c => c.path === 'Conversion' || c.path === 'ReturnToUnity').length);
  readonly communionCount = computed(() => this.cases().filter(c => c.path === 'Communion').length);

  constructor(private readonly dokCasesService: DokCasesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.dokCasesService.search().subscribe(result => this.cases.set(result.items));
  }

  openAddForm(): void {
    this.formValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: DokCaseFormValue): void {
    this.dokCasesService.create(value).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  pathLabel(path: string): string {
    return PATH_LABELS[path] ?? path;
  }
}
