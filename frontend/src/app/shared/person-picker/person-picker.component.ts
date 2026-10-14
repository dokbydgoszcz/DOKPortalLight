import { Component, ElementRef, HostListener, OnDestroy, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Subject, Subscription, switchMap } from 'rxjs';
import { PeopleService } from '../../features/people/people.service';
import { FunctionType, Person } from '../../features/people/person.model';

/** Ile podpowiedzi pokazuje lista. */
export const PICKER_LIMIT = 15;

/**
 * Wybór osoby z wyszukiwaniem: wpisujesz fragment nazwiska, a lista podpowiada pasujące osoby (zamiast
 * długiej listy rozwijanej z limitem). Wartość to id osoby ('' = nikt), więc działa z [(ngModel)].
 */
@Component({
  selector: 'app-person-picker',
  standalone: true,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PersonPickerComponent), multi: true }],
  template: `
    <div class="picker">
      <input
        type="text"
        role="combobox"
        autocomplete="off"
        [attr.aria-expanded]="open()"
        [attr.aria-label]="placeholder()"
        [placeholder]="placeholder()"
        [value]="text()"
        [disabled]="disabled()"
        (input)="onType($any($event.target).value)"
        (focus)="onFocus()"
        (keydown)="onKey($event)" />
      @if (selectedId() && !disabled()) {
        <button type="button" class="picker-clear" title="Wyczyść" aria-label="Wyczyść wybór" (click)="clear()">×</button>
      }
      @if (open()) {
        <ul class="picker-options" role="listbox">
          @for (person of options(); track person.id; let i = $index) {
            <li role="option" [class.active]="i === active()" [attr.aria-selected]="person.id === selectedId()" (mousedown)="$event.preventDefault()" (click)="choose(person)">
              <span>{{ person.fullName }}</span>
              @if (person.email) { <small>{{ person.email }}</small> }
            </li>
          } @empty {
            <li class="picker-empty">{{ loading() ? 'Szukam…' : 'Nikogo nie znaleziono.' }}</li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    :host{display:block}
    .picker{position:relative}
    .picker input{padding-right:30px}
    .picker-clear{position:absolute;right:6px;top:50%;transform:translateY(-50%);border:0;background:none;font-size:18px;line-height:1;color:#6C7A75;cursor:pointer;padding:2px 6px}
    .picker-options{position:absolute;z-index:30;left:0;right:0;top:calc(100% + 4px);margin:0;padding:4px;list-style:none;background:white;border:1px solid var(--line);border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,.12);max-height:260px;overflow:auto}
    .picker-options li{padding:8px 10px;border-radius:8px;cursor:pointer;display:flex;flex-direction:column;font-size:13px}
    .picker-options li small{color:#6C7A75;font-size:11px}
    .picker-options li.active,.picker-options li:hover{background:#EEF4F1}
    .picker-options li.picker-empty{cursor:default;color:#6C7A75}
  `]
})
export class PersonPickerComponent implements ControlValueAccessor, OnDestroy {
  readonly placeholder = input('Wpisz nazwisko…');
  /** Tylko osoby z tą funkcją (np. 'Pastor'). */
  readonly fn = input<FunctionType | undefined>(undefined);
  /** Osoby, których nie podpowiadać (np. już przypisane). */
  readonly exclude = input<readonly string[]>([]);

  readonly text = signal('');
  readonly selectedId = signal('');
  readonly options = signal<Person[]>([]);
  readonly open = signal(false);
  readonly active = signal(-1);
  readonly loading = signal(false);
  readonly disabled = signal(false);

  private readonly people = inject(PeopleService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly queries = new Subject<string>();
  private readonly sub: Subscription;
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    this.sub = this.queries
      .pipe(switchMap(query => this.people.search(query, 1, PICKER_LIMIT + this.exclude().length, this.fn())))
      .subscribe({
        next: result => {
          const excluded = new Set(this.exclude());
          this.options.set(result.items.filter(p => !excluded.has(p.id)).slice(0, PICKER_LIMIT));
          this.loading.set(false);
          this.active.set(-1);
        },
        error: () => {
          this.options.set([]);
          this.loading.set(false);
        }
      });
  }

  writeValue(value: string | null | undefined): void {
    const id = value ?? '';
    if (id === this.selectedId()) return;
    this.selectedId.set(id);
    if (!id) {
      this.text.set('');
      return;
    }
    const known = this.options().find(p => p.id === id);
    if (known) {
      this.text.set(known.fullName);
      return;
    }
    this.people.getById(id).subscribe({
      next: person => {
        if (this.selectedId() === id) this.text.set(person.fullName);
      },
      error: () => this.text.set('(osoba niedostępna)')
    });
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  onFocus(): void {
    this.search(this.selectedId() ? '' : this.text());
  }

  onType(value: string): void {
    this.text.set(value);
    if (this.selectedId()) {
      // Zmiana tekstu odpina wybraną osobę – trzeba wybrać ponownie z listy.
      this.selectedId.set('');
      this.onChange('');
    }
    this.search(value);
  }

  private search(query: string): void {
    this.loading.set(true);
    this.open.set(true);
    this.queries.next(query.trim());
  }

  choose(person: Person): void {
    this.selectedId.set(person.id);
    this.text.set(person.fullName);
    this.open.set(false);
    this.onChange(person.id);
    this.onTouched();
  }

  clear(): void {
    this.selectedId.set('');
    this.text.set('');
    this.onChange('');
    this.onTouched();
  }

  onKey(event: KeyboardEvent): void {
    const count = this.options().length;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!this.open()) {
        this.onFocus();
        return;
      }
      const step = event.key === 'ArrowDown' ? 1 : -1;
      this.active.set(count ? (this.active() + step + count) % count : -1);
    } else if (event.key === 'Enter' && this.open() && this.active() >= 0) {
      event.preventDefault();
      this.choose(this.options()[this.active()]);
    } else if (event.key === 'Escape') {
      this.open.set(false);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }
}
