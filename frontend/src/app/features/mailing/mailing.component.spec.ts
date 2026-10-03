import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { MailingComponent } from './mailing.component';
import { MailingCampaign } from './mailing-campaign.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const draft: MailingCampaign = {
  id: 'c1', subject: 'Zaproszenie', body: 'Treść', group: 'DokCases', recipientCount: 12,
  status: 'Draft', createdAtUtc: '2026-09-22T00:00:00Z', sentAtUtc: null
};
const sent: MailingCampaign = { ...draft, id: 'c2', subject: 'Podsumowanie', group: 'CandidatesSksp', status: 'Sent', sentAtUtc: '2026-09-23T00:00:00Z' };
const url = api('/api/mailing/campaigns');

function boot(items: MailingCampaign[] = [draft, sent]) {
  const ctx = setup(MailingComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('MailingComponent', () => {
  it('renders campaigns with their group, status and recipients', () => {
    const { el } = boot();

    const text = textOf(el);
    expect(text).toContain('Zaproszenie');
    expect(text).toContain('Podopieczni DOK');
    expect(text).toContain('Kandydaci SKŚP');
    expect(text).toContain('Szkic');
    expect(text).toContain('Wysłano');
  });

  it('offers sending only for drafts', () => {
    const { el } = boot();

    const sendButtons = Array.from(el.querySelectorAll('tbody button')).filter(b => b.textContent!.includes('Wyślij'));
    expect(sendButtons.length).toBe(1);
  });

  it('shows a toast when the campaigns cannot be loaded', () => {
    const { fixture, http } = setup(MailingComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy kampanii.');
  });

  describe('adding a campaign', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowa kampania');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('offers every recipient group and posts the typed campaign', async () => {
      const ctx = await openForm();
      expect(ctx.el.querySelectorAll('select[name="group"] option').length).toBe(4);

      setInput(ctx.el, 'input[name="subject"]', 'Rekolekcje');
      setSelect(ctx.el, 'select[name="group"]', 'Missionaries');
      setInput(ctx.el, 'textarea[name="body"]', 'Zapraszamy w sobotę.');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ subject: 'Rekolekcje', body: 'Zapraszamy w sobotę.', group: 'Missionaries' });
      req.flush(draft);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano kampanię.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([draft]);
    });

    it('defaults to the candidates group, shows a toast when adding fails and closes on cancel', async () => {
      const ctx = await openForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      const req = ctx.http.expectOne(r => r.method === 'POST');
      expect(req.request.body.group).toBe('CandidatesSksp');
      req.flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać kampanii.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('sending a campaign', () => {
    it('sends the draft, confirms and reloads', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === `${url}/c1/send`);
      expect(req.request.body).toEqual({});
      req.flush({ ...draft, status: 'Sent' });

      expect(toastMessages()).toContain('Kampania wysłana.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([sent]);
    });

    it('shows the message from the server when sending is refused', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      ctx.http.expectOne(r => r.method === 'POST')
        .flush({ title: 'Wysyłanie e-maili nie jest skonfigurowane (brak Smtp:Host).' }, { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('Wysyłanie e-maili nie jest skonfigurowane (brak Smtp:Host).');
    });

    it('falls back to a generic message when the server gives no reason', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Wyślij');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się wysłać kampanii.');
    });
  });
});
