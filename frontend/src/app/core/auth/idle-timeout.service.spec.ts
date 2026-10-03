import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IdleTimeoutService } from './idle-timeout.service';
import { AuthService } from './auth.service';

const MINUTE = 60 * 1000;

describe('IdleTimeoutService', () => {
  let service: IdleTimeoutService;
  let logout: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    logout = vi.fn();
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: { logout } }] });
    service = TestBed.inject(IdleTimeoutService);
  });

  afterEach(() => {
    service.stop();
    vi.useRealTimers();
  });

  it('logs the user out for inactivity after twenty minutes', () => {
    service.start();

    vi.advanceTimersByTime(20 * MINUTE - 1);
    expect(logout).not.toHaveBeenCalled();

    vi.advanceTimersByTime(2);
    expect(logout).toHaveBeenCalledTimes(1);
    expect(logout).toHaveBeenCalledWith('idle');
  });

  it('restarts the countdown on user activity', () => {
    service.start();
    vi.advanceTimersByTime(15 * MINUTE);

    document.dispatchEvent(new Event('mousedown'));
    vi.advanceTimersByTime(15 * MINUTE);
    expect(logout).not.toHaveBeenCalled();

    vi.advanceTimersByTime(5 * MINUTE + 1);
    expect(logout).toHaveBeenCalledTimes(1);
  });

  it('reacts to keyboard, scroll and touch activity as well', () => {
    service.start();

    for (const eventName of ['keydown', 'scroll', 'touchstart', 'mousemove']) {
      vi.advanceTimersByTime(19 * MINUTE);
      document.dispatchEvent(new Event(eventName));
    }

    expect(logout).not.toHaveBeenCalled();
  });

  it('stops counting and ignores activity after stop()', () => {
    service.start();

    service.stop();
    vi.advanceTimersByTime(60 * MINUTE);
    document.dispatchEvent(new Event('mousedown'));
    vi.advanceTimersByTime(60 * MINUTE);

    expect(logout).not.toHaveBeenCalled();
  });

  it('can be stopped safely when it was never started', () => {
    expect(() => service.stop()).not.toThrow();
  });
});
