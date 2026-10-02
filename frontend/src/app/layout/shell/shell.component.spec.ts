import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/auth/auth.service';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;

  function setup(permissions: string[]) {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            roles: () => [],
            hasPermission: (p: string) => permissions.includes(p),
            logout: vi.fn()
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  it('hides the admin nav items for a user without the matching permissions', () => {
    setup(['Meetings.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).not.toContain('Użytkownicy i role');
    expect(text).not.toContain('Audit log');
  });

  it('shows the admin nav item for a user with Users.Manage', () => {
    setup(['Users.Manage']);

    expect((fixture.nativeElement.textContent as string)).toContain('Użytkownicy i role');
  });

  it('always shows the items that need no permission and only the permitted modules', () => {
    setup(['Meetings.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Baza osób');
    expect(text).toContain('Harmonogram i obecności');
    expect(text).not.toContain('Budżet SKŚP');
  });
});
