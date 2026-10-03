import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeetingsService } from './meetings.service';
import { CreateMeetingValue, Meeting, MeetingAttendee } from './meeting.model';
import { ToastService } from '../../core/notifications/toast.service';
import { DokCasesService } from '../dok-cases/dok-cases.service';
import { DokCase } from '../dok-cases/dok-case.model';

@Component({
  selector: 'app-meetings-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, FormsModule],
  templateUrl: './meetings-list.component.html',
  styleUrl: './meetings-list.component.scss'
})
export class MeetingsListComponent implements OnInit {
  readonly meetings = signal<Meeting[]>([]);
  readonly dokCases = signal<DokCase[]>([]);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly useAttendees = signal(false);
  /** Wybrani uczestnicy zajęć grupowych: id sprawy → zapisana obecność (zachowywana przy edycji). */
  readonly attendeeSelection = signal<Record<string, boolean | null>>({});
  readonly expandedIds = signal<ReadonlySet<string>>(new Set());
  newMeeting: CreateMeetingValue = { groupLabel: '', meetingDate: '' };

  constructor(
    private readonly meetingsService: MeetingsService,
    private readonly dokCasesService: DokCasesService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadDokCases();
  }

  private loadDokCases(): void {
    this.dokCasesService.search(undefined, 1, 1000).subscribe({
      next: result => this.dokCases.set(result.items),
      error: () => this.toast.error('Nie udało się wczytać listy spraw DOK.')
    });
  }

  onDokCaseChange(value: string): void {
    this.newMeeting.dokCaseId = value || undefined;
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
    this.useAttendees.set(false);
    this.attendeeSelection.set({});
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
    this.useAttendees.set(meeting.attendees.length > 0);
    this.attendeeSelection.set(Object.fromEntries(meeting.attendees.map(a => [a.dokCaseId, a.isAttended])));
    this.isFormOpen.set(true);
  }

  onUseAttendeesChange(checked: boolean): void {
    this.useAttendees.set(checked);
    if (checked) {
      this.newMeeting.dokCaseId = undefined;
    }
  }

  isAttendeeSelected(dokCaseId: string): boolean {
    return dokCaseId in this.attendeeSelection();
  }

  toggleAttendeeSelection(dokCaseId: string, checked: boolean): void {
    this.attendeeSelection.update(selection => {
      const next = { ...selection };
      if (checked) {
        next[dokCaseId] = dokCaseId in selection ? selection[dokCaseId] : null;
      } else {
        delete next[dokCaseId];
      }
      return next;
    });
  }

  createMeeting(): void {
    const id = this.editingId();
    const value: CreateMeetingValue = {
      ...this.newMeeting,
      attendees: this.useAttendees()
        ? Object.entries(this.attendeeSelection()).map(([dokCaseId, isAttended]) => ({ dokCaseId, isAttended: isAttended ?? undefined }))
        : undefined
    };
    const request$ = id ? this.meetingsService.update(id, value) : this.meetingsService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : 'Dodano spotkanie.');
        this.load();
      },
      error: () => this.toast.error(id ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać spotkania.')
    });
  }

  setAttendance(meeting: Meeting, value: boolean): void {
    const next = meeting.isAttended === value ? null : value;
    this.meetingsService.setAttendance(meeting.id, next).subscribe({
      next: updated => this.meetings.update(list => list.map(m => (m.id === updated.id ? updated : m))),
      error: () => this.toast.error('Nie udało się zapisać obecności.')
    });
  }

  setAttendeeAttendance(meeting: Meeting, attendee: MeetingAttendee, value: boolean): void {
    const next = attendee.isAttended === value ? null : value;
    this.meetingsService.setAttendeeAttendance(meeting.id, attendee.dokCaseId, next).subscribe({
      next: updated => this.meetings.update(list => list.map(m => (m.id === updated.id ? updated : m))),
      error: () => this.toast.error('Nie udało się zapisać obecności.')
    });
  }

  isExpanded(id: string): boolean {
    return this.expandedIds().has(id);
  }

  toggleExpanded(id: string): void {
    this.expandedIds.update(ids => {
      const next = new Set(ids);
      if (!next.delete(id)) {
        next.add(id);
      }
      return next;
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
