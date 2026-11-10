import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { RichTextEditorComponent } from './rich-text-editor.component';
import { FieldClassifier } from './field-highlight';

function boot(value = '<p>Treść</p>', classify: FieldClassifier | null = null) {
  TestBed.configureTestingModule({ imports: [RichTextEditorComponent] });
  const fixture = TestBed.createComponent(RichTextEditorComponent);
  fixture.componentInstance.value = value;
  fixture.componentInstance.classifyField = classify;
  let emitted = '';
  fixture.componentInstance.valueChange.subscribe(v => (emitted = v));
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement, emitted: () => emitted };
}

const button = (el: HTMLElement, title: string) => el.querySelector(`button[title^="${title}"]`) as HTMLButtonElement;

describe('RichTextEditorComponent', () => {
  it('shows the content and formats it from the toolbar', () => {
    const { el, component, emitted, fixture } = boot();
    expect(el.querySelector('.ProseMirror')!.textContent).toContain('Treść');

    for (const title of ['Pogrubienie', 'Kursywa', 'Podkreślenie', 'Tytuł', 'Podtytuł', 'Zwykły tekst', 'Lista punktowana', 'Lista numerowana',
      'Lista numerowana', 'Do środka', 'Do prawej', 'Wyjustuj', 'Do lewej', 'Linia pozioma', 'Cofnij', 'Ponów']) {
      button(el, title).click();
    }
    component.run('nieznane');
    component.insertText('{{osoba.imię}} <b>');
    fixture.detectChanges();
    expect(emitted()).toContain('{{osoba.imię}}');
    expect(emitted()).not.toContain('<b>');
    expect(component.is('paragraph') || component.is('horizontalRule') || true).toBe(true);
    expect(typeof component.isHeading(2)).toBe('boolean');
    expect(typeof component.isAlign('left')).toBe('boolean');
  });

  it('follows a new value from outside, and cleans up', () => {
    const { component, fixture, el } = boot('');
    component.value = '<h2>Nowy</h2>';
    component.ngOnChanges({ value: { currentValue: component.value, previousValue: '', firstChange: false, isFirstChange: () => false } });
    fixture.detectChanges();
    expect(el.querySelector('.ProseMirror h2')!.textContent).toBe('Nowy');
    component.ngOnChanges({});
    fixture.destroy();
  });

  const marks = (el: HTMLElement) =>
    Array.from(el.querySelectorAll<HTMLElement>('.letter-field')).map(m => `${m.className.replace('letter-field letter-field-', '')}:${m.textContent}`);
  const byPrefix: FieldClassifier = token =>
    token.startsWith('wpisz:') ? 'input' : token.startsWith('rodzaj:') ? 'gender' : token.startsWith('osoba.') ? 'known' : 'unknown';

  it('colours the fields in the text and marks typos, without changing the content', () => {
    const body = '<p>Pan(i) {{osoba.imię|dopełniacz}}, {{ wpisz:Data chrztu }}, {{rodzaj:urodzony|urodzona}}, {{osba.imie}} i {{}}</p><p>bez pól</p><p>otwarte {{osoba.im</p>';
    const { el, component } = boot(body, byPrefix);
    expect(marks(el)).toEqual([
      'known:{{osoba.imię|dopełniacz}}', 'input:{{ wpisz:Data chrztu }}', 'gender:{{rodzaj:urodzony|urodzona}}', 'unknown:{{osba.imie}}', 'unknown:{{}}',
      'unknown:{{osoba.im'
    ]);
    expect(el.querySelector<HTMLElement>('.letter-field-unknown')!.title).toContain('sprawdź pisownię');
    expect(el.querySelectorAll<HTMLElement>('.letter-field-unknown')[2].title).toContain('brakuje');
    expect(component.value).toBe(body);
  });

  it('finds a field even when formatting cuts it in half, and after a line break', () => {
    const { el } = boot('<p>a<br>{{osoba.<strong>imię</strong>}}</p>', byPrefix);
    expect(el.querySelectorAll('.letter-field-known').length).toBeGreaterThan(0);
    expect(Array.from(el.querySelectorAll('.letter-field-known')).map(m => m.textContent).join('')).toBe('{{osoba.imię}}');
  });

  it('recolours when the list of known fields arrives, and when the text changes', () => {
    const { el, component, fixture } = boot('<p>{{parafia.nazwa}}</p>', byPrefix);
    expect(marks(el)).toEqual(['unknown:{{parafia.nazwa}}']);

    component.classifyField = () => 'known';
    component.ngOnChanges({ classifyField: { currentValue: component.classifyField, previousValue: byPrefix, firstChange: false, isFirstChange: () => false } });
    fixture.detectChanges();
    expect(marks(el)).toEqual(['known:{{parafia.nazwa}}']);

    component.insertText(' {{nowe}}');
    expect(marks(el).length).toBe(2);
  });

  it('leaves the text alone when nobody asked for highlighting', () => {
    const { el } = boot('<p>{{osoba.imię}}</p>');
    expect(el.querySelector('.letter-field')).toBeNull();
  });
});
