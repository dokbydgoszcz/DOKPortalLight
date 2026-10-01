import { Component, OnInit, signal } from '@angular/core';
import { DokCasesService } from './dok-cases.service';
import { DokCase, DokCaseFormValue } from './dok-case.model';
import { DokCaseFormComponent } from './dok-case-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PATH_LABELS: Record<string, string> = {
  BaptismCandidate: 'Chrzest',
  Confirmation: 'Bierzmowanie',
  Communion: 'Stół Pański',
  Conversion: 'Konwersja',
  ReturnToUnity: 'Powrót do Jedności'
};

const PAGE_SIZE = 20;

@Component({
  selector: 'app-dok-cases-list',
  standalone: true,
  imports: [DokCaseFormComponent, PaginationComponent],
  templateUrl: './dok-cases-list.component.html',
  styleUrl: './dok-cases-list.component.scss'
})
export class DokCasesListComponent implements OnInit {
  readonly cases = signal<DokCase[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  formValue: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };

  readonly baptismCount = signal(0);
  readonly confirmationCount = signal(0);
  readonly conversionCount = signal(0);
  readonly communionCount = signal(0);

  constructor(
    private readonly dokCasesService: DokCasesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadStats();
  }

  load(): void {
    this.dokCasesService.search(undefined, this.page(), this.pageSize).subscribe({
      next: result => {
        this.cases.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy podopiecznych.')
    });
  }

  private loadStats(): void {
    this.dokCasesService.search(undefined, 1, 1000).subscribe(result => {
      const all = result.items;
      this.baptismCount.set(all.filter(c => c.path === 'BaptismCandidate').length);
      this.confirmationCount.set(all.filter(c => c.path === 'Confirmation').length);
      this.conversionCount.set(all.filter(c => c.path === 'Conversion' || c.path === 'ReturnToUnity').length);
      this.communionCount.set(all.filter(c => c.path === 'Communion').length);
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.formValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: DokCaseFormValue): void {
    this.dokCasesService.create(value).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano podopiecznego.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się dodać podopiecznego.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  pathLabel(path: string): string {
    return PATH_LABELS[path] ?? path;
  }

  deleteCase(dokCase: DokCase): void {
    if (!confirm(`Usunąć podopiecznego „${dokCase.personFullName}”?`)) return;
    this.dokCasesService.delete(dokCase.id).subscribe({
      next: () => {
        this.toast.success('Podopieczny usunięty.');
        this.load();
        this.loadStats();
      },
      error: () => this.toast.error('Nie udało się usunąć podopiecznego.')
    });
  }
}
