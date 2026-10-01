import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeetingsService } from './meetings.service';
import { CreateMeetingValue, Meeting } from './meeting.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-meetings-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './meetings-list.component.html',
  styleUrl: './meetings-list.component.scss'
})
export class MeetingsListComponent implements OnInit {
  readonly meetings = signal<Meeting[]>([]);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  newMeeting: CreateMeetingValue = { groupLabel: '', meetingDate: '' };

  constructor(
    private readonly meetingsService: MeetingsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.meetingsService.list().subscribe({
      next: meetings => this.meetings.set(meetings),
      error: () => this.toast.error('Nie udało się wczytać listy spotkań.')
    });
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newMeeting = { groupLabel: '', meetingDate: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(meeting: Meeting): void {
    this.editingId.set(meeting.id);
    this.newMeeting = {
      dokCaseId: meeting.dokCaseId ?? undefined,
      groupLabel: meeting.groupLabel ?? undefined,
      meetingDate: meeting.meetingDate,
      isAttended: meeting.isAttended ?? undefined,
      notes: meeting.notes ?? undefined
    };
    this.isFormOpen.set(true);
  }

  createMeeting(): void {
    const id = this.editingId();
    const request$ = id ? this.meetingsService.update(id, this.newMeeting) : this.meetingsService.create(this.newMeeting);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano spotkanie.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać spotkania.')
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteMeeting(meeting: Meeting): void {
    if (!confirm(`Usunąć spotkanie „${meeting.caseLabel ?? meeting.groupLabel}” z dnia ${meeting.meetingDate}?`)) return;
    this.meetingsService.delete(meeting.id).subscribe({
      next: () => {
        this.toast.success('Spotkanie usunięte.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć spotkania.')
    });
  }
}
