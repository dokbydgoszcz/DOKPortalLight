export interface RoleInfo {
  name: string;
  isSystem: boolean;
}

export interface PermissionInfo {
  name: string;
  module: string;
  label: string;
}

export interface PermissionMatrix {
  roles: RoleInfo[];
  permissions: PermissionInfo[];
  grants: Record<string, string[]>;
}
