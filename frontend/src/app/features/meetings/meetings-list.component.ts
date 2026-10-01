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
    this.newMeeting = { groupLabel: '', meetingDate: '' };
    this.isFormOpen.set(true);
  }

  createMeeting(): void {
    this.meetingsService.create(this.newMeeting).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano spotkanie.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać spotkania.')
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
