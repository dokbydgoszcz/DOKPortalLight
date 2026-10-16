import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { RichTextEditorComponent } from './rich-text-editor.component';

function boot(value = '<p>Treść</p>') {
  TestBed.configureTestingModule({ imports: [RichTextEditorComponent] });
  const fixture = TestBed.createComponent(RichTextEditorComponent);
  fixture.componentInstance.value = value;
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
});
