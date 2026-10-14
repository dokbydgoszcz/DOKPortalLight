import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { ExportButtonComponent } from '../../shared/export/export-button.component';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MeetingsCalendarComponent } from './meetings-calendar.component';
import { CalendarHowtoComponent } from '../../shared/calendar-howto/calendar-howto.component';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { FormsModule } from '@angular/forms';
import { MeetingsService } from './meetings.service';
import { CreateMeetingValue, DEFAULT_END, DEFAULT_START, Meeting, MeetingAttendee, hhmm, timeRange, weeklyCount } from './meeting.model';
import { ToastService } from '../../core/notifications/toast.service';
import { DokCasesService } from '../dok-cases/dok-cases.service';
import { DokCase } from '../dok-cases/dok-case.model';

const VIEW_KEY = 'dokportal.meetingsView';

function readView(): 'list' | 'calendar' {
  try {
    return localStorage.getItem(VIEW_KEY) === 'calendar' ? 'calendar' : 'list';
  } catch {
    return 'list';
  }
}

@Component({
  selector: 'app-meetings-list',
  standalone: true,
  imports: [HasPermissionDirective, ExportButtonComponent, FormsModule, MeetingsCalendarComponent, CalendarHowtoComponent],
  templateUrl: './meetings-list.component.html',
  styleUrl: './meetings-list.component.scss'
})
export class MeetingsListComponent implements OnInit {
  readonly meetings = signal<Meeting[]>([]);
  readonly dokCases = signal<DokCase[]>([]);
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly useAttendees = signal(false);
  /** Nowe spotkanie jako seria co tydzień. */
  readonly repeatWeekly = signal(false);
  /** Wybrani uczestnicy zajęć grupowych: id sprawy → zapisana obecność (zachowywana przy edycji). */
  readonly attendeeSelection = signal<Record<string, boolean | null>>({});
  readonly expandedIds = signal<ReadonlySet<string>>(new Set());
  newMeeting: CreateMeetingValue = { groupLabel: '', meetingDate: '' };
  readonly timeRange = timeRange;
  /** Lista albo kalendarz; wybór zapamiętuje przeglądarka. */
  readonly view = signal<'list' | 'calendar'>(readView());
  readonly canManage = inject(AuthService).hasPermission(Permissions.MeetingsManage);

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

  setView(view: 'list' | 'calendar'): void {
    this.view.set(view);
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      // brak dostępu do pamięci przeglądarki – widok i tak działa
    }
  }

  /** Klik w spotkanie w kalendarzu: edycja (z uprawnieniem) albo szczegóły na liście. */
  onCalendarMeeting(meeting: Meeting): void {
    if (this.canManage) {
      this.openEditForm(meeting);
      return;
    }
    this.toast.success(`${meeting.caseLabel ?? meeting.groupLabel} – ${meeting.meetingDate}${meeting.notes ? ': ' + meeting.notes : ''}`);
  }

  /** Klik w pusty dzień kalendarza: nowe spotkanie z tą datą. */
  openAddFormForDate(value: string): void {
    this.openAddForm();
    this.newMeeting.meetingDate = value.slice(0, 10);
    // Klik w godzinę w widoku tygodnia: spotkanie od tej godziny, na dwie godziny.
    if (value.length > 10) {
      const start = value.slice(11, 16);
      const endHour = Math.min(Number(start.slice(0, 2)) + 2, 23);
      this.newMeeting.startTime = start;
      this.newMeeting.endTime = `${String(endHour).padStart(2, '0')}:${endHour === 23 && Number(start.slice(0, 2)) + 2 > 23 ? '59' : start.slice(3, 5)}`;
    }
  }

  /** Błąd godzin widoczny od razu (koniec przed początkiem, koniec bez początku). */
  get timeProblem(): string | null {
    const { startTime, endTime } = this.newMeeting;
    if (endTime && !startTime) return 'Podaj godzinę rozpoczęcia.';
    if (startTime && endTime && endTime <= startTime) return 'Koniec musi być później niż początek.';
    return null;
  }

  /** Liczba spotkań w serii (przy zaznaczonym „Powtarzaj co tydzień”). */
  get seriesCount(): number {
    return weeklyCount(this.newMeeting.meetingDate, this.newMeeting.repeatWeeklyUntil);
  }

  get repeatProblem(): string | null {
    if (!this.repeatWeekly() || this.editingId()) return null;
    if (!this.newMeeting.repeatWeeklyUntil) return 'Podaj, do kiedy powtarzać.';
    if (this.seriesCount < 2) return 'Data końca musi być co najmniej tydzień po pierwszym spotkaniu.';
    if (this.seriesCount > 53) return 'Seria może trwać najwyżej rok.';
    return null;
  }

  onRepeatWeeklyChange(checked: boolean): void {
    this.repeatWeekly.set(checked);
    if (!checked) this.newMeeting.repeatWeeklyUntil = undefined;
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.newMeeting = { groupLabel: '', meetingDate: '', startTime: DEFAULT_START, endTime: DEFAULT_END };
    this.useAttendees.set(false);
    this.repeatWeekly.set(false);
    this.attendeeSelection.set({});
    this.isFormOpen.set(true);
  }

  openEditForm(meeting: Meeting): void {
    this.editingId.set(meeting.id);
    this.repeatWeekly.set(false);
    this.newMeeting = {
      dokCaseId: meeting.dokCaseId ?? undefined,
      groupLabel: meeting.groupLabel ?? undefined,
      meetingDate: meeting.meetingDate,
      startTime: hhmm(meeting.startTime) || undefined,
      endTime: hhmm(meeting.endTime) || undefined,
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
      startTime: this.newMeeting.startTime || undefined,
      endTime: this.newMeeting.endTime || undefined,
      repeatWeeklyUntil: !id && this.repeatWeekly() ? this.newMeeting.repeatWeeklyUntil : undefined,
      attendees: this.useAttendees()
        ? Object.entries(this.attendeeSelection()).map(([dokCaseId, isAttended]) => ({ dokCaseId, isAttended: isAttended ?? undefined }))
        : undefined
    };
    const count = value.repeatWeeklyUntil ? this.seriesCount : 1;
    const request$ = id ? this.meetingsService.update(id, value) : this.meetingsService.create(value);
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(id ? 'Zapisano zmiany.' : count > 1 ? `Dodano ${count} spotkań (co tydzień).` : 'Dodano spotkanie.');
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
    const following = meeting.seriesId
      ? this.meetings().filter(m => m.seriesId === meeting.seriesId && m.meetingDate > meeting.meetingDate).length
      : 0;
    const withFollowing = following > 0 && confirm(`To spotkanie należy do serii. Usunąć też ${following} kolejnych spotkań z tej serii?`);
    this.meetingsService.delete(meeting.id, withFollowing).subscribe({
      next: () => {
        this.toast.success(withFollowing ? `Usunięto ${following + 1} spotkań z serii.` : 'Spotkanie usunięte.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć spotkania.')
    });
  }
}
