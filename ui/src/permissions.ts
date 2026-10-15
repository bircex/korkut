import type { User } from "./api";

// The permission keys the app shows or hides things by. The server checks every call anyway;
// this only keeps a member from seeing doors they cannot open (docs/MATTER_RBAC_PLAN.md).
export const P = {
  mattersRead: "matters.read",
  mattersCreate: "matters.create",
  chatUse: "chat.use",
  documentsRead: "documents.read",
  agendaRead: "agenda.read",
  clientsRead: "clients.read",
  financeRead: "finance.read",
  reportsRead: "reports.read",
  researchUse: "research.use",
  membersManage: "members.manage",
  rolesManage: "roles.manage",
  officeSettings: "office.settings",
  integrationsManage: "integrations.manage",
} as const;

export function can(user: Pick<User, "permissions"> | null | undefined, permission: string): boolean {
  return !!user?.permissions?.includes(permission);
}
