import { describe, expect, it, vi } from 'vitest';
import { NAV_ITEMS } from '../../layout/nav-items';
import { clickByText, setup } from '../../testing/test-helpers';
import { HELP_AUDIENCES, HELP_TOPICS } from './help-content';
import { HelpComponent } from './help.component';

function boot(granted: string[]) {
  const ctx = setup(HelpComponent, { granted });
  ctx.fixture.detectChanges();
  return ctx;
}

const titles = (el: HTMLElement) => Array.from(el.querySelectorAll('.topic h3')).map(h => h.textContent!.trim());

describe('HelpComponent', () => {
  it('opens on the part for catechists when the account only runs its own cases', () => {
    const { el } = boot(['DokCases.View', 'Meetings.Manage']);
    expect(el.querySelector('.tab.active')!.textContent).toContain('Katechista prowadzący');
    expect(titles(el)).toEqual(HELP_TOPICS.catechist.map(t => t.title));
    expect(el.textContent).toContain('Podopieczni DOK → Notatki');
    expect(el.querySelector('a[href="/meetings"]')).not.toBeNull();
  });

  it('opens on the part for directors, and switches between the parts', () => {
    const { el, fixture } = boot(['Candidates.Manage']);
    expect(el.querySelector('.tab.active')!.textContent).toContain('Dyrektor');
    expect(titles(el)).toContain('Wiadomość do grupy (mailing)');

    clickByText(el, 'Konto i logowanie', 'button');
    fixture.detectChanges();
    expect(titles(el)).toEqual(HELP_TOPICS.account.map(t => t.title));
    expect(el.textContent).toContain('Nie pamiętam hasła');
    // Temat bez ekranu do otwarcia nie ma linku.
    const logout = Array.from(el.querySelectorAll('.topic')).find(t => t.querySelector('h3')!.textContent === 'Wylogowanie')!;
    expect(logout.querySelector('a')).toBeNull();
  });

  it('prints the instructions', () => {
    const { el } = boot(['*']);
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    clickByText(el, 'Drukuj', 'button');
    expect(print).toHaveBeenCalled();
    print.mockRestore();
  });

  it('points only at screens that exist in the menu or the account page', () => {
    const known = new Set([...NAV_ITEMS.map(i => i.path), '/konto/haslo']);
    for (const audience of HELP_AUDIENCES) {
      for (const topic of HELP_TOPICS[audience.id]) {
        if (topic.link) expect(known.has(topic.link), `${topic.title}: ${topic.link}`).toBe(true);
        expect(topic.steps.length).toBeGreaterThan(0);
      }
    }
    expect(NAV_ITEMS.some(i => i.path === '/pomoc' && !i.permission)).toBe(true);
  });
});
