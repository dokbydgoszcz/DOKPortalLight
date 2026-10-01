import { Component, OnInit, signal } from '@angular/core';
import { MissionsService } from './missions.service';
import { Mission, MissionFormValue } from './mission.model';
import { MissionFormComponent } from './mission-form.component';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-missions-list',
  standalone: true,
  imports: [MissionFormComponent],
  templateUrl: './missions-list.component.html',
  styleUrl: './missions-list.component.scss'
})
export class MissionsListComponent implements OnInit {
  readonly missions = signal<Mission[]>([]);
  readonly isFormOpen = signal(false);
  formValue: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };

  constructor(
    private readonly missionsService: MissionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.missionsService.search().subscribe({
      next: result => this.missions.set(result.items),
      error: () => this.toast.error('Nie udało się wczytać listy misji.')
    });
  }

  openAddForm(): void {
    this.formValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
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
