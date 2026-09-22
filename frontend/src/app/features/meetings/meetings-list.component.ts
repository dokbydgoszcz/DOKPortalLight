import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeetingsService } from './meetings.service';
import { CreateMeetingValue, Meeting } from './meeting.model';

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

  constructor(private readonly meetingsService: MeetingsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.meetingsService.list().subscribe(meetings => this.meetings.set(meetings));
  }

  openAddForm(): void {
    this.newMeeting = { groupLabel: '', meetingDate: '' };
    this.isFormOpen.set(true);
  }

  createMeeting(): void {
    this.meetingsService.create(this.newMeeting).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
