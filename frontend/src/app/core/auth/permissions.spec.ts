import { describe, it, expect } from 'vitest';
import { ALL_PERMISSIONS, Permissions } from './permissions';

describe('Permissions', () => {
  it('has 41 unique names in Module.Action format', () => {
    expect(ALL_PERMISSIONS.length).toBe(41);
    expect(new Set(ALL_PERMISSIONS).size).toBe(41);
    for (const name of ALL_PERMISSIONS) {
      expect(name).toMatch(/^[A-Za-z]+\.[A-Za-z]+$/);
    }
  });

  it('exposes the constants used by the menu and guards', () => {
    expect(Permissions.PeopleManage).toBe('People.Manage');
    expect(Permissions.BudgetDokView).toBe('BudgetDok.View');
    expect(Permissions.PermissionsManage).toBe('Permissions.Manage');
  });
});
