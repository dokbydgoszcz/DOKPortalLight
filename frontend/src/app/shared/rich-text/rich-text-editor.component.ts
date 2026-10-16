import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild, signal } from '@angular/core';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Underline from '@tiptap/extension-underline';
import TextAlign from '@tiptap/extension-text-align';

type Align = 'left' | 'center' | 'right' | 'justify';

/** Prosty edytor tekstu pism (TipTap): akapity, nagłówki, pogrubienie, kursywa, podkreślenie, listy, wyrównanie, linia. */
@Component({
  selector: 'app-rich-text-editor',
  standalone: true,
  template: `
    <div class="rte">
      <div class="toolbar" role="toolbar" aria-label="Formatowanie">
        <button type="button" title="Pogrubienie (Ctrl+B)" [class.on]="is('bold')" (click)="run('bold')"><b>B</b></button>
        <button type="button" title="Kursywa (Ctrl+I)" [class.on]="is('italic')" (click)="run('italic')"><i>I</i></button>
        <button type="button" title="Podkreślenie (Ctrl+U)" [class.on]="is('underline')" (click)="run('underline')"><u>U</u></button>
        <span class="sep"></span>
        <button type="button" title="Zwykły tekst" [class.on]="is('paragraph')" (click)="run('paragraph')">¶</button>
        <button type="button" title="Tytuł" [class.on]="isHeading(2)" (click)="run('h2')">T1</button>
        <button type="button" title="Podtytuł" [class.on]="isHeading(3)" (click)="run('h3')">T2</button>
        <span class="sep"></span>
        <button type="button" title="Lista punktowana" [class.on]="is('bulletList')" (click)="run('bullets')">• ≡</button>
        <button type="button" title="Lista numerowana" [class.on]="is('orderedList')" (click)="run('numbers')">1. ≡</button>
        <span class="sep"></span>
        <button type="button" title="Do lewej" [class.on]="isAlign('left')" (click)="align('left')">⇤</button>
        <button type="button" title="Do środka" [class.on]="isAlign('center')" (click)="align('center')">↔</button>
        <button type="button" title="Do prawej" [class.on]="isAlign('right')" (click)="align('right')">⇥</button>
        <button type="button" title="Wyjustuj" [class.on]="isAlign('justify')" (click)="align('justify')">☰</button>
        <span class="sep"></span>
        <button type="button" title="Linia pozioma" (click)="run('rule')">―</button>
        <button type="button" title="Cofnij (Ctrl+Z)" (click)="run('undo')">↶</button>
        <button type="button" title="Ponów (Ctrl+Y)" (click)="run('redo')">↷</button>
      </div>
      <div #host class="sheet-area"></div>
    </div>
  `,
  styles: [`
    .rte { border: 1px solid var(--line, #dfe5e2); border-radius: 10px; background: #f3f5f4; overflow: hidden; }
    .toolbar { display: flex; flex-wrap: wrap; gap: 2px; padding: 6px; background: white; border-bottom: 1px solid var(--line, #dfe5e2); position: sticky; top: 0; z-index: 1; }
    .toolbar button { min-width: 30px; height: 28px; padding: 0 6px; border: 1px solid transparent; border-radius: 6px; background: transparent; cursor: pointer; font-size: 13px; color: #28413D; }
    .toolbar button:hover { background: #EEF2EF; }
    .toolbar button.on { background: #DCEBE7; border-color: #b9d4cd; }
    .sep { width: 1px; margin: 4px 4px; background: var(--line, #dfe5e2); }
    .sheet-area { max-height: 62vh; overflow: auto; padding: 18px 0; }
    :host ::ng-deep .ProseMirror { width: min(100%, 680px); min-height: 420px; margin: 0 auto; padding: 48px 56px; background: white;
      box-shadow: 0 1px 3px rgba(0,0,0,.12); outline: none; font-size: 14px; line-height: 1.5; color: #1d2522; }
    :host ::ng-deep .ProseMirror p { margin: 0 0 6px; min-height: 1em; }
    :host ::ng-deep .ProseMirror h2 { font-size: 20px; margin: 10px 0 8px; }
    :host ::ng-deep .ProseMirror h3 { font-size: 16px; margin: 8px 0 6px; }
    :host ::ng-deep .ProseMirror ul, :host ::ng-deep .ProseMirror ol { padding-left: 24px; margin: 0 0 6px; }
    :host ::ng-deep .ProseMirror hr { border: 0; border-top: 1px solid #999; margin: 10px 0; }
    @media (max-width: 700px) { :host ::ng-deep .ProseMirror { padding: 20px; } }
  `]
})
export class RichTextEditorComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() value = '';
  @Output() readonly valueChange = new EventEmitter<string>();
  @ViewChild('host', { static: true }) private readonly host!: ElementRef<HTMLElement>;

  private editor?: Editor;
  /** Zmienia się przy każdej zmianie zaznaczenia – odświeża stan przycisków. */
  readonly tick = signal(0);

  ngAfterViewInit(): void {
    this.editor = new Editor({
      element: this.host.nativeElement,
      extensions: [StarterKit.configure({ heading: { levels: [2, 3] }, code: false, codeBlock: false, blockquote: false, strike: false }), Underline,
        TextAlign.configure({ types: ['heading', 'paragraph'] })],
      content: this.value || '<p></p>',
      onUpdate: ({ editor }) => {
        this.value = editor.getHTML();
        this.valueChange.emit(this.value);
        this.tick.update(t => t + 1);
      },
      onSelectionUpdate: () => this.tick.update(t => t + 1)
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['value'] && this.editor && this.value !== this.editor.getHTML()) {
      this.editor.commands.setContent(this.value || '<p></p>', false);
    }
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
  }

  /** Wstawia tekst (np. pole {{osoba.imię}}) w miejscu kursora. */
  insertText(text: string): void {
    this.editor?.chain().focus().insertContent(text.replace(/&/g, '&amp;').replace(/</g, '&lt;')).run();
  }

  is(name: string): boolean {
    this.tick();
    return !!this.editor?.isActive(name);
  }

  isHeading(level: number): boolean {
    this.tick();
    return !!this.editor?.isActive('heading', { level });
  }

  isAlign(value: Align): boolean {
    this.tick();
    return !!this.editor?.isActive({ textAlign: value });
  }

  align(value: Align): void {
    this.editor?.chain().focus().setTextAlign(value).run();
  }

  run(command: string): void {
    const chain = this.editor?.chain().focus();
    if (!chain) return;
    switch (command) {
      case 'bold': chain.toggleBold().run(); break;
      case 'italic': chain.toggleItalic().run(); break;
      case 'underline': chain.toggleUnderline().run(); break;
      case 'paragraph': chain.setParagraph().run(); break;
      case 'h2': chain.toggleHeading({ level: 2 }).run(); break;
      case 'h3': chain.toggleHeading({ level: 3 }).run(); break;
      case 'bullets': chain.toggleBulletList().run(); break;
      case 'numbers': chain.toggleOrderedList().run(); break;
      case 'rule': chain.setHorizontalRule().run(); break;
      case 'undo': chain.undo().run(); break;
      case 'redo': chain.redo().run(); break;
    }
  }
}
