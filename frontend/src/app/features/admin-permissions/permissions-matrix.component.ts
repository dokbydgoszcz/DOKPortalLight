import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ToastService } from '../../core/notifications/toast.service';
import { PermissionInfo, PermissionMatrix } from './permissions.model';
import { PermissionsService } from './permissions.service';

interface ModuleGroup {
  module: string;
  permissions: PermissionInfo[];
}

function sameSet(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every(x => b.includes(x));
}

function cloneGrants(matrix: PermissionMatrix): Record<string, string[]> {
  const copy: Record<string, string[]> = {};
  for (const role of matrix.roles) {
    copy[role.name] = [...(matrix.grants[role.name] ?? [])];
  }
  return copy;
}

@Component({
  selector: 'app-permissions-matrix',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './permissions-matrix.component.html',
  styleUrl: './permissions-matrix.component.scss'
})
export class PermissionsMatrixComponent implements OnInit {
  readonly matrix = signal<PermissionMatrix | null>(null);
  readonly draft = signal<Record<string, string[]>>({});
  readonly saving = signal(false);
  newRoleName = '';

  readonly modules = computed<ModuleGroup[]>(() => {
    const groups: ModuleGroup[] = [];
    for (const permission of this.matrix()?.permissions ?? []) {
      let group = groups.find(g => g.module === permission.module);
      if (!group) {
        group = { module: permission.module, permissions: [] };
        groups.push(group);
      }
      group.permissions.push(permission);
    }
    return groups;
  });

  readonly dirtyRoles = computed(() => {
    const matrix = this.matrix();
    if (!matrix) return [];
    const draft = this.draft();
    return matrix.roles.map(r => r.name).filter(name => !sameSet(matrix.grants[name] ?? [], draft[name] ?? []));
  });

  readonly hasChanges = computed(() => this.dirtyRoles().length > 0);

  constructor(
    private readonly permissionsService: PermissionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.permissionsService.getMatrix().subscribe({
      next: matrix => {
        this.matrix.set(matrix);
        this.draft.set(cloneGrants(matrix));
      },
      error: () => this.toast.error('Nie udało się wczytać uprawnień.')
    });
  }

  isGranted(role: string, permission: string): boolean {
    return (this.draft()[role] ?? []).includes(permission);
  }

  toggle(role: string, permission: string, checked: boolean): void {
    this.draft.update(draft => {
      const current = draft[role] ?? [];
      const next = checked ? [...new Set([...current, permission])] : current.filter(p => p !== permission);
      return { ...draft, [role]: next };
    });
  }

  discard(): void {
    const matrix = this.matrix();
    if (matrix) {
      this.draft.set(cloneGrants(matrix));
    }
  }

  save(): void {
    const dirty = this.dirtyRoles();
    if (dirty.length === 0) return;
    this.saving.set(true);
    forkJoin(dirty.map(role => this.permissionsService.updateRole(role, this.draft()[role]))).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(`Zapisano uprawnienia: ${dirty.join(', ')}.`);
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(err?.error?.title ?? 'Nie udało się zapisać uprawnień.');
        this.load();
      }
    });
  }

  createRole(): void {
    const name = this.newRoleName.trim();
    if (!name) return;
    this.permissionsService.createRole(name).subscribe({
      next: () => {
        this.newRoleName = '';
        this.toast.success(`Dodano rolę „${name}”.`);
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się dodać roli.')
    });
  }

  deleteRole(role: string): void {
    if (!confirm(`Usunąć rolę „${role}”?`)) return;
    this.permissionsService.deleteRole(role).subscribe({
      next: () => {
        this.toast.success(`Usunięto rolę „${role}”.`);
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się usunąć roli.')
    });
  }
}
