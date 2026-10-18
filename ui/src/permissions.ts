import { createContext, useContext } from "react";
import type { User } from "./api";

// The permission keys the app shows or hides things by. The server checks every call anyway;
// this only keeps a member from seeing doors they cannot open (docs/MATTER_RBAC_PLAN.md).
export const P = {
  mattersRead: "matters.read",
  mattersCreate: "matters.create",
  mattersEdit: "matters.edit",
  mattersDelete: "matters.delete",
  chatUse: "chat.use",
  chatGeneral: "chat.general",
  documentsRead: "documents.read",
  documentsWrite: "documents.write",
  documentsDelete: "documents.delete",
  agendaRead: "agenda.read",
  agendaWrite: "agenda.write",
  notesRead: "notes.read",
  notesWrite: "notes.write",
  clientsRead: "clients.read",
  clientsWrite: "clients.write",
  financeRead: "finance.read",
  financeWrite: "finance.write",
  reportsRead: "reports.read",
  researchUse: "research.use",
  membersManage: "members.manage",
  rolesManage: "roles.manage",
  officeSettings: "office.settings",
  integrationsManage: "integrations.manage",
  auditRead: "audit.read",
} as const;

export function can(user: Pick<User, "permissions"> | null | undefined, permission: string): boolean {
  return !!user?.permissions?.includes(permission);
}

// The signed-in member's permissions for components deep in the tree, so a button for something
// they may not do is not shown (Workspace provides it; the server refuses such calls anyway).
export const PermissionsContext = createContext<readonly string[]>([]);

export function useCan(permission: string): boolean {
  return useContext(PermissionsContext).includes(permission);
}
