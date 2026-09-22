import { Component, OnInit, signal } from '@angular/core';
import { MissionsService } from './missions.service';
import { Mission, MissionFormValue } from './mission.model';
import { MissionFormComponent } from './mission-form.component';

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

  constructor(private readonly missionsService: MissionsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.missionsService.search().subscribe(result => this.missions.set(result.items));
  }

  openAddForm(): void {
    this.formValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: MissionFormValue): void {
    this.missionsService.create(value).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  statusPillClass(status: string): string {
    if (status === 'wygasła') return 'pill red';
    if (status === 'wygasa') return 'pill red';
    return 'pill green';
  }
}
