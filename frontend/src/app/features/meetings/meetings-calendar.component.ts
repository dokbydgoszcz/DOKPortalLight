import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, effect, input, output } from '@angular/core';
import { Calendar, EventInput } from '@fullcalendar/core';
import plLocale from '@fullcalendar/core/locales/pl';
import dayGridPlugin from '@fullcalendar/daygrid';
import interactionPlugin from '@fullcalendar/interaction';
import listPlugin from '@fullcalendar/list';
import timeGridPlugin from '@fullcalendar/timegrid';
import { Meeting, hhmm } from './meeting.model';

/** Kolor spotkania: obecny / nieobecny / jeszcze nieoznaczone; zajęcia grupowe osobnym kolorem. */
export function meetingColor(meeting: Meeting): { className: string; status: string } {
  if (meeting.attendees.length > 0) {
    const marked = meeting.attendees.filter(a => a.isAttended !== null).length;
    const present = meeting.attendees.filter(a => a.isAttended === true).length;
    return marked === 0
      ? { className: 'mc-group', status: `zajęcia grupowe, ${meeting.attendees.length} os.` }
      : { className: 'mc-group', status: `zajęcia grupowe, obecni ${present}/${meeting.attendees.length}` };
  }
  if (meeting.isAttended === true) return { className: 'mc-present', status: 'obecny' };
  if (meeting.isAttended === false) return { className: 'mc-absent', status: 'nieobecny' };
  return { className: 'mc-pending', status: 'obecność nieoznaczona' };
}

/** Spotkania jako wydarzenia kalendarza; bez godziny – całodniowe. */
export function toCalendarEvents(meetings: Meeting[]): EventInput[] {
  return meetings.map(m => {
    const { className, status } = meetingColor(m);
    const start = hhmm(m.startTime);
    const end = hhmm(m.endTime);
    return {
      id: m.id,
      title: m.caseLabel ?? m.groupLabel ?? 'Spotkanie',
      start: start ? `${m.meetingDate}T${start}` : m.meetingDate,
      end: start && end ? `${m.meetingDate}T${end}` : undefined,
      allDay: !start,
      classNames: [className],
      extendedProps: { status, notes: m.notes }
    };
  });
}

/** Widok kalendarza harmonogramu (FullCalendar): miesiąc, tydzień i lista; klik w spotkanie lub dzień. */
@Component({
  selector: 'app-meetings-calendar',
  standalone: true,
  template: `
    <div class="legend" aria-label="Legenda kolorów">
      <span><i class="mc-present"></i>obecny</span>
      <span><i class="mc-absent"></i>nieobecny</span>
      <span><i class="mc-pending"></i>nieoznaczone</span>
      <span><i class="mc-group"></i>zajęcia grupowe</span>
    </div>
    <div #host class="calendar-host"></div>
  `,
  styleUrl: './meetings-calendar.component.scss'
})
export class MeetingsCalendarComponent implements AfterViewInit, OnDestroy {
  readonly meetings = input.required<Meeting[]>();
  /** Czy kliknięcie pustego dnia ma zaproponować nowe spotkanie. */
  readonly canAdd = input(false);
  readonly meetingClick = output<Meeting>();
  readonly dayClick = output<string>();

  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLElement>;
  private calendar: Calendar | null = null;

  constructor() {
    effect(() => {
      const events = toCalendarEvents(this.meetings());
      if (!this.calendar) return;
      this.calendar.removeAllEvents();
      this.calendar.addEventSource(events);
    });
  }

  ngAfterViewInit(): void {
    this.calendar = new Calendar(this.host.nativeElement, {
      plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin, listPlugin],
      locale: plLocale,
      // Na wąskim ekranie siatka miesiąca ucina nazwiska – zaczynamy od listy.
      initialView: typeof window !== 'undefined' && window.innerWidth < 640 ? 'listMonth' : 'dayGridMonth',
      headerToolbar: { left: 'prev,next today', center: 'title', right: 'dayGridMonth,timeGridWeek,listMonth' },
      slotMinTime: '07:00:00',
      slotMaxTime: '22:00:00',
      allDayText: 'cały dzień',
      eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
      displayEventEnd: true,
      buttonText: { today: 'dziś', month: 'miesiąc', week: 'tydzień', list: 'lista' },
      height: 'auto',
      dayMaxEvents: 4,
      fixedWeekCount: false,
      events: toCalendarEvents(this.meetings()),
      eventClick: info => {
        info.jsEvent.preventDefault();
        const meeting = this.meetings().find(m => m.id === info.event.id);
        if (meeting) this.meetingClick.emit(meeting);
      },
      dateClick: info => {
        if (this.canAdd()) this.dayClick.emit(info.dateStr);
      },
      eventDidMount: info => {
        const status = info.event.extendedProps['status'] as string;
        const notes = info.event.extendedProps['notes'] as string | null;
        info.el.title = [info.event.title, status, notes].filter(Boolean).join(' — ');
      }
    });
    this.calendar.render();
  }

  ngOnDestroy(): void {
    this.calendar?.destroy();
  }
}
