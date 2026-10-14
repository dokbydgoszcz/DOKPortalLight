import { describe, expect, it } from 'vitest';
import { meetingColor, toCalendarEvents } from './meetings-calendar.component';
import { Meeting } from './meeting.model';

const base: Meeting = { id: 'm1', dokCaseId: 'c1', caseLabel: 'Jan Kowalski', groupLabel: null, meetingDate: '2026-10-14', isAttended: null, notes: 'Rozmowa', attendees: [] };

describe('meetings calendar', () => {
  it('colours individual meetings by attendance', () => {
    expect(meetingColor({ ...base, isAttended: true }).className).toBe('mc-present');
    expect(meetingColor({ ...base, isAttended: false }).className).toBe('mc-absent');
    expect(meetingColor(base)).toEqual({ className: 'mc-pending', status: 'obecność nieoznaczona' });
  });

  it('shows group meetings with the number of people present', () => {
    const group = { ...base, caseLabel: null, groupLabel: 'Grupa A', attendees: [
      { dokCaseId: 'a', personFullName: 'A', isAttended: true },
      { dokCaseId: 'b', personFullName: 'B', isAttended: false },
      { dokCaseId: 'c', personFullName: 'C', isAttended: null }] };
    expect(meetingColor(group)).toEqual({ className: 'mc-group', status: 'zajęcia grupowe, obecni 1/3' });
    expect(meetingColor({ ...group, attendees: group.attendees.map(a => ({ ...a, isAttended: null })) }).status).toBe('zajęcia grupowe, 3 os.');
  });

  it('turns meetings into all-day events titled with the person or group', () => {
    const [individual, group, unnamed] = toCalendarEvents([
      base,
      { ...base, id: 'm2', caseLabel: null, groupLabel: 'Grupa A' },
      { ...base, id: 'm3', caseLabel: null, groupLabel: null }
    ]);
    expect(individual).toMatchObject({ id: 'm1', title: 'Jan Kowalski', start: '2026-10-14', allDay: true, classNames: ['mc-pending'] });
    expect(individual.extendedProps).toEqual({ status: 'obecność nieoznaczona', notes: 'Rozmowa' });
    expect(group.title).toBe('Grupa A');
    expect(unnamed.title).toBe('Spotkanie');
  });

  it('places meetings with hours in the time grid', () => {
    const [timed, startOnly] = toCalendarEvents([
      { ...base, startTime: '18:00:00', endTime: '20:00:00' },
      { ...base, id: 'm2', startTime: '09:30:00', endTime: null }
    ]);
    expect(timed).toMatchObject({ start: '2026-10-14T18:00', end: '2026-10-14T20:00', allDay: false });
    expect(startOnly).toMatchObject({ start: '2026-10-14T09:30', end: undefined, allDay: false });
  });
});
