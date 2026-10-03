import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MeetingsListComponent } from './meetings-list.component';
import { Meeting } from './meeting.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const groupMeeting: Meeting = { id: 'm1', dokCaseId: null, caseLabel: null, groupLabel: 'DOK grupa', meetingDate: '2026-09-24', isAttended: null, notes: null, attendees: [] };
const caseMeeting: Meeting = { id: 'm2', dokCaseId: 'c1', caseLabel: 'Jan Kowalski', groupLabel: null, meetingDate: '2026-09-25', isAttended: true, notes: 'ok', attendees: [] };
const attendeesMeeting: Meeting = {
  id: 'm3', dokCaseId: null, caseLabel: null, groupLabel: 'Grupa wieczorna', meetingDate: '2026-09-26', isAttended: null, notes: null,
  attendees: [
    { dokCaseId: 'c1', personFullName: 'Jan Kowalski', isAttended: true },
    { dokCaseId: 'c2', personFullName: 'Ewa Zielińska', isAttended: null }
  ]
};
const dokCases = [{
  id: 'c1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation',
  catechistPersonId: 'k1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null
}, {
  id: 'c2', personId: 'p2', personFullName: 'Ewa Zielińska', parishName: null, path: 'Confirmation', stage: 'Formation',
  catechistPersonId: 'k1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null
}];
const url = api('/api/meetings');
const casesUrl = api('/api/dok-cases');

function boot(meetings: Meeting[] = [groupMeeting, caseMeeting]) {
  const ctx = setup(MeetingsListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(meetings);
  ctx.http.expectOne(r => r.url === casesUrl && r.params.get('pageSize') === '1000').flush({ items: dokCases, totalCount: 2, page: 1, pageSize: 1000 });
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

describe('MeetingsListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders meetings returned from the API', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('DOK grupa');
    expect(textOf(el)).toContain('Jan Kowalski');
  });

  it('shows toasts when the meetings or the DOK cases cannot be loaded', () => {
    const { fixture, http } = setup(MeetingsListComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url === casesUrl).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy spotkań.');
    expect(toastMessages()).toContain('Nie udało się wczytać listy spraw DOK.');
  });

  it('treats an empty selection as a group meeting and a chosen case as an individual one', () => {
    const { fixture } = boot();
    const component = fixture.componentInstance;

    component.onDokCaseChange('c1');
    expect(component.newMeeting.dokCaseId).toBe('c1');
    component.onDokCaseChange('');
    expect(component.newMeeting.dokCaseId).toBeUndefined();
  });

  describe('adding a meeting', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj spotkanie');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('posts a group meeting without a case', async () => {
      const ctx = await openForm();
      expect(textOf(ctx.el)).toContain('Nowe spotkanie');

      setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-15');
      setInput(ctx.el, 'input[name="groupLabel"]', 'Grupa wieczorna');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body.meetingDate).toBe('2026-10-15');
      expect(req.request.body.groupLabel).toBe('Grupa wieczorna');
      expect(req.request.body.dokCaseId).toBeUndefined();
      req.flush(groupMeeting);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano spotkanie.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([groupMeeting]);
    });

    it('posts the chosen DOK case with an individual meeting', async () => {
      const ctx = await openForm();

      setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-16');
      setSelect(ctx.el, 'select[name="dokCaseId"]', 'c1');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body.dokCaseId).toBe('c1');
      req.flush(caseMeeting);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([caseMeeting]);
    });

    it('keeps saving disabled until a date is chosen, so an empty date is never sent', async () => {
      const ctx = await openForm();
      expect(saveButton(ctx.el).disabled).toBe(true);
      expect(textOf(ctx.el)).toContain('Uzupełnij pola oznaczone *');
      expect(ctx.el.querySelector('.field .required')).not.toBeNull();

      saveButton(ctx.el).click();
      ctx.http.expectNone(r => r.method === 'POST');

      setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-10');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);
      expect(textOf(ctx.el)).not.toContain('Uzupełnij pola oznaczone *');
    });

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-10');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać spotkania.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('editing a meeting', () => {
    async function openEdit() {
      const ctx = boot([caseMeeting]);
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the meeting and sends the change with PUT', async () => {
      const ctx = await openEdit();
      expect(textOf(ctx.el)).toContain('Edytuj spotkanie');
      expect((ctx.el.querySelector('input[name="meetingDate"]') as HTMLInputElement).value).toBe('2026-09-25');
      expect((ctx.el.querySelector('select[name="dokCaseId"]') as HTMLSelectElement).value).toBe('c1');

      setInput(ctx.el, 'input[name="meetingDate"]', '2026-09-30');
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m2`);
      expect(req.request.body).toEqual({ dokCaseId: 'c1', groupLabel: undefined, meetingDate: '2026-09-30', isAttended: true, notes: 'ok' });
      req.flush(caseMeeting);
      expect(toastMessages()).toContain('Zapisano zmiany.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([caseMeeting]);
    });

    it('shows a different toast when the update fails', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
    });
  });

  describe('deleting a meeting', () => {
    it('names the case or group and the date in the confirmation, then deletes and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([groupMeeting, caseMeeting]);

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć spotkanie „DOK grupa” z dnia 2026-09-24?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/m1`).flush(null);

      expect(toastMessages()).toContain('Spotkanie usunięte.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([caseMeeting]);
    });

    it('does nothing when the confirmation is cancelled', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć spotkania.');
    });
  });

  describe('quick attendance toggle', () => {
    const rowOf = (el: HTMLElement, text: string) =>
      Array.from(el.querySelectorAll('tr')).find(r => r.textContent!.includes(text))! as HTMLElement;
    const toggle = (el: HTMLElement, rowText: string, label: string) =>
      Array.from(rowOf(el, rowText).querySelectorAll<HTMLButtonElement>('.attendance-toggle button')).find(b => b.textContent!.trim() === label)!;

    it('marks a meeting as attended and shows the new state without reloading the list', () => {
      const ctx = boot();

      toggle(ctx.el, 'DOK grupa', 'Obecny').click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m1/attendance`);
      expect(req.request.body).toEqual({ isAttended: true });
      req.flush({ ...groupMeeting, isAttended: true });
      ctx.fixture.detectChanges();

      expect(rowOf(ctx.el, 'DOK grupa').querySelector('.pill.green')).not.toBeNull();
      expect(toggle(ctx.el, 'DOK grupa', 'Obecny').classList.contains('active')).toBe(true);
    });

    it('marks a meeting as absent', () => {
      const ctx = boot();

      toggle(ctx.el, 'DOK grupa', 'Nieobecny').click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m1/attendance`);
      expect(req.request.body).toEqual({ isAttended: false });
      req.flush({ ...groupMeeting, isAttended: false });
      ctx.fixture.detectChanges();

      expect(rowOf(ctx.el, 'DOK grupa').querySelector('.pill.red')).not.toBeNull();
    });

    it('clears the attendance when the active button is clicked again', () => {
      const ctx = boot();

      toggle(ctx.el, 'Jan Kowalski', 'Obecny').click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m2/attendance`);
      expect(req.request.body).toEqual({ isAttended: null });
      req.flush({ ...caseMeeting, isAttended: null });
      ctx.fixture.detectChanges();

      expect(rowOf(ctx.el, 'Jan Kowalski').querySelector('.pill')).toBeNull();
    });

    it('shows a toast and keeps the old state when saving fails', () => {
      const ctx = boot();

      toggle(ctx.el, 'DOK grupa', 'Obecny').click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Nie udało się zapisać obecności.');
      expect(rowOf(ctx.el, 'DOK grupa').querySelector('.pill')).toBeNull();
    });

    it('is hidden for users who cannot manage meetings', () => {
      const ctx = setup(MeetingsListComponent, { granted: ['Meetings.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([groupMeeting, caseMeeting]);
      ctx.http.expectOne(r => r.url === casesUrl).flush({ items: dokCases, totalCount: 2, page: 1, pageSize: 1000 });
      ctx.fixture.detectChanges();

      expect(ctx.el.querySelector('.attendance-toggle')).toBeNull();
    });
  });

  describe('group meetings with attendees', () => {
    const rowByText = (el: HTMLElement, text: string) =>
      Array.from(el.querySelectorAll('tr')).find(r => r.textContent!.includes(text)) as HTMLElement | undefined;
    const attendeeToggle = (el: HTMLElement, name: string, label: string) =>
      Array.from(rowByText(el, name)!.querySelectorAll<HTMLButtonElement>('.attendance-toggle button')).find(b => b.textContent!.trim() === label)!;

    function bootWithAttendees() {
      const ctx = boot([attendeesMeeting, caseMeeting]);
      clickByText(ctx.el, 'Uczestnicy (2)');
      ctx.fixture.detectChanges();
      return ctx;
    }

    it('hides the attendees until expanded, then lists them with their state', () => {
      const ctx = boot([attendeesMeeting]);
      expect(textOf(ctx.el)).not.toContain('Ewa Zielińska');

      clickByText(ctx.el, 'Uczestnicy (2)');
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('Ewa Zielińska');
      expect(rowByText(ctx.el, 'Jan Kowalski')!.querySelector('.pill.green')).not.toBeNull();
      expect(rowByText(ctx.el, 'Ewa Zielińska')!.querySelector('.pill')).toBeNull();

      clickByText(ctx.el, 'Uczestnicy (2)');
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).not.toContain('Ewa Zielińska');
    });

    it('has no meeting-level attendance buttons for a meeting with attendees', () => {
      const ctx = boot([attendeesMeeting]);

      const mainRow = rowByText(ctx.el, 'Grupa wieczorna')!;
      expect(mainRow.querySelector('.attendance-toggle')).toBeNull();
    });

    it('marks an attendee as present and updates only that attendee', () => {
      const ctx = bootWithAttendees();

      attendeeToggle(ctx.el, 'Ewa Zielińska', 'Obecny').click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m3/attendees/c2`);
      expect(req.request.body).toEqual({ isAttended: true });
      req.flush({ ...attendeesMeeting, attendees: [attendeesMeeting.attendees[0], { ...attendeesMeeting.attendees[1], isAttended: true }] });
      ctx.fixture.detectChanges();

      expect(rowByText(ctx.el, 'Ewa Zielińska')!.querySelector('.pill.green')).not.toBeNull();
    });

    it('clears an attendee when the active button is clicked again', () => {
      const ctx = bootWithAttendees();

      attendeeToggle(ctx.el, 'Jan Kowalski', 'Obecny').click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m3/attendees/c1`);
      expect(req.request.body).toEqual({ isAttended: null });
      req.flush({ ...attendeesMeeting, attendees: [{ ...attendeesMeeting.attendees[0], isAttended: null }, attendeesMeeting.attendees[1]] });
    });

    it('shows a toast when saving an attendee fails', () => {
      const ctx = bootWithAttendees();

      attendeeToggle(ctx.el, 'Ewa Zielińska', 'Nieobecny').click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się zapisać obecności.');
    });

    describe('form', () => {
      const attendeeCheckbox = (el: HTMLElement, name: string) =>
        Array.from(el.querySelectorAll<HTMLLabelElement>('.attendee-picker label')).find(l => l.textContent!.includes(name))!
          .querySelector('input') as HTMLInputElement;

      async function openForm() {
        const ctx = boot();
        clickByText(ctx.el, 'Dodaj spotkanie');
        ctx.fixture.detectChanges();
        await ctx.fixture.whenStable();
        return ctx;
      }

      it('swaps the single-case select for an attendee picker when the group mode is switched on', async () => {
        const ctx = await openForm();
        expect(ctx.el.querySelector('select[name="dokCaseId"]')).not.toBeNull();
        expect(ctx.el.querySelector('.attendee-picker')).toBeNull();

        (ctx.el.querySelector('input[name="useAttendees"]') as HTMLInputElement).click();
        ctx.fixture.detectChanges();

        expect(ctx.el.querySelector('select[name="dokCaseId"]')).toBeNull();
        expect(ctx.el.querySelectorAll('.attendee-picker input').length).toBe(2);
        expect(ctx.el.querySelector('input[name="useAttendees"]')!.closest('label')!.classList.contains('checkbox-label')).toBe(true);
        expect(ctx.el.querySelector('.attendee-picker input')!.closest('label')!.classList.contains('checkbox-label')).toBe(true);
      });

      it('posts the chosen attendees and no single case', async () => {
        const ctx = await openForm();
        setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-20');
        setInput(ctx.el, 'input[name="groupLabel"]', 'Grupa czwartkowa');
        (ctx.el.querySelector('input[name="useAttendees"]') as HTMLInputElement).click();
        ctx.fixture.detectChanges();
        attendeeCheckbox(ctx.el, 'Jan Kowalski').click();
        attendeeCheckbox(ctx.el, 'Ewa Zielińska').click();
        ctx.fixture.detectChanges();

        saveButton(ctx.el).click();

        const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
        expect(req.request.body.dokCaseId).toBeUndefined();
        expect(req.request.body.attendees).toEqual([
          { dokCaseId: 'c1', isAttended: undefined },
          { dokCaseId: 'c2', isAttended: undefined }
        ]);
        req.flush(attendeesMeeting);
        ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([attendeesMeeting]);
      });

      it('unchecking an attendee removes them from the request', async () => {
        const ctx = await openForm();
        setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-20');
        (ctx.el.querySelector('input[name="useAttendees"]') as HTMLInputElement).click();
        ctx.fixture.detectChanges();
        attendeeCheckbox(ctx.el, 'Jan Kowalski').click();
        attendeeCheckbox(ctx.el, 'Ewa Zielińska').click();
        attendeeCheckbox(ctx.el, 'Jan Kowalski').click();
        ctx.fixture.detectChanges();

        saveButton(ctx.el).click();

        const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
        expect(req.request.body.attendees).toEqual([{ dokCaseId: 'c2', isAttended: undefined }]);
        req.flush(attendeesMeeting);
        ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([attendeesMeeting]);
      });

      it('opens a group meeting for editing with its attendees checked and keeps their attendance', async () => {
        const ctx = boot([attendeesMeeting]);
        clickByText(ctx.el, 'Edytuj');
        ctx.fixture.detectChanges();
        await ctx.fixture.whenStable();

        expect((ctx.el.querySelector('input[name="useAttendees"]') as HTMLInputElement).checked).toBe(true);
        expect(attendeeCheckbox(ctx.el, 'Jan Kowalski').checked).toBe(true);
        expect(attendeeCheckbox(ctx.el, 'Ewa Zielińska').checked).toBe(true);

        attendeeCheckbox(ctx.el, 'Ewa Zielińska').click();
        ctx.fixture.detectChanges();
        saveButton(ctx.el).click();

        const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m3`);
        expect(req.request.body.attendees).toEqual([{ dokCaseId: 'c1', isAttended: true }]);
        req.flush(attendeesMeeting);
        ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([attendeesMeeting]);
      });
    });
  });

  describe('meeting notes', () => {
    it('lets you type notes for a new meeting and posts them', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj spotkanie');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      setInput(ctx.el, 'input[name="meetingDate"]', '2026-10-21');
      setInput(ctx.el, 'textarea[name="notes"]', 'Omówiono rozdział 3; kolejne spotkanie za tydzień.');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body.notes).toBe('Omówiono rozdział 3; kolejne spotkanie za tydzień.');
      req.flush(groupMeeting);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([groupMeeting]);
    });

    it('shows the existing notes in the edit form and saves a changed note', async () => {
      const ctx = boot([caseMeeting]);
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      expect((ctx.el.querySelector('textarea[name="notes"]') as HTMLTextAreaElement).value).toBe('ok');

      setInput(ctx.el, 'textarea[name="notes"]', 'Zmieniona notatka');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/m2`);
      expect(req.request.body.notes).toBe('Zmieniona notatka');
      req.flush(caseMeeting);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([caseMeeting]);
    });
  });
});
