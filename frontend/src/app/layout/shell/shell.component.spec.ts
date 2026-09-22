import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/auth/auth.service';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;

  function setup(roles: string[]) {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            roles: () => roles,
            hasAnyRole: (required: string[]) => required.some(r => roles.includes(r)),
            logout: vi.fn()
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  it('hides the admin nav item for a user without the Administrator role', () => {
    setup(['KatechistaProwadzacy']);
    expect((fixture.nativeElement.textContent as string)).not.toContain('Użytkownicy i role');
  });

  it('shows the admin nav item for an Administrator', () => {
    setup(['Administrator']);
    expect((fixture.nativeElement.textContent as string)).toContain('Użytkownicy i role');
  });
});
