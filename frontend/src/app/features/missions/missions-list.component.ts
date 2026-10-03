import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { MissionsService } from './missions.service';
import { Mission, MissionFormValue } from './mission.model';
import { MissionFormComponent } from './mission-form.component';
import { ToastService } from '../../core/notifications/toast.service';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-missions-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, MissionFormComponent, PaginationComponent],
  templateUrl: './missions-list.component.html',
  styleUrl: './missions-list.component.scss'
})
export class MissionsListComponent implements OnInit {
  readonly missions = signal<Mission[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = PAGE_SIZE;
  readonly isFormOpen = signal(false);
  formValue: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '', sentToDok: false };

  constructor(
    private readonly missionsService: MissionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.missionsService.search('', this.page(), this.pageSize).subscribe({
      next: result => {
        this.missions.set(result.items);
        this.totalCount.set(result.totalCount);
      },
      error: () => this.toast.error('Nie udało się wczytać listy misji.')
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openAddForm(): void {
    this.formValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '', sentToDok: false };
    this.isFormOpen.set(true);
  }

  onSave(value: MissionFormValue): void {
    this.missionsService.create(value).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano misję.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać misji.')
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  deleteMission(mission: Mission): void {
    if (!confirm(`Usunąć misję „${mission.personFullName}”?`)) return;
    this.missionsService.delete(mission.id).subscribe({
      next: () => {
        this.toast.success('Misja usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć misji.')
    });
  }

  statusPillClass(status: string): string {
    if (status === 'wygasła') return 'pill red';
    if (status === 'wygasa') return 'pill red';
    return 'pill green';
  }
}
