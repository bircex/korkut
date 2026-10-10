/* eslint-disable react-refresh/only-export-components -- error predicates colocated with the state components that use them */
import type { ReactNode } from "react";
import { ApiError } from "../api";
import { LoadingOverlay, ErrorState } from "../a11y";

// The five standard async states every migrated office-core screen must handle
// (KDM-030): loading, error, empty, permission-denied (403) and concurrent-update
// (409). Screens render <AsyncBoundary> around their data so the states are
// consistent and accessible everywhere.

export type AsyncStatus = "loading" | "error" | "ready";

export function isPermissionDenied(err: unknown): boolean {
  return err instanceof ApiError && err.status === 403;
}

export function isConcurrentUpdate(err: unknown): boolean {
  return err instanceof ApiError && err.status === 409;
}

export function PermissionDenied({ children }: { children?: ReactNode }) {
  return (
    <div role="alert" className="rounded-[10px] border border-amber-200 bg-amber-50 p-4 text-[13px] text-amber-800">
      <p className="m-0 font-semibold">Bu işlem için yetkiniz yok</p>
      {children && <p className="m-0 mt-1">{children}</p>}
    </div>
  );
}

export function ConcurrentUpdate({ onReload }: { onReload?: () => void }) {
  return (
    <ErrorState title="Kayıt bu sırada değişti" onRetry={onReload}>
      Bu kaydı başka biri güncelledi. Değişikliklerinizi kaybetmemek için son hali yeniden yükleyin.
    </ErrorState>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="rounded-[10px] border border-dashed border-zinc-300 p-8 text-center text-[13px] text-zinc-500">
      <p className="m-0 font-medium text-zinc-600">{title}</p>
      {children && <p className="m-0 mt-1">{children}</p>}
    </div>
  );
}

export function AsyncBoundary({
  status,
  error,
  isEmpty,
  emptyTitle = "Kayıt yok",
  onRetry,
  children,
}: {
  status: AsyncStatus;
  error?: unknown;
  isEmpty?: boolean;
  emptyTitle?: string;
  onRetry?: () => void;
  children: ReactNode;
}) {
  if (status === "loading") return <LoadingOverlay />;
  if (status === "error") {
    if (isPermissionDenied(error)) return <PermissionDenied />;
    if (isConcurrentUpdate(error)) return <ConcurrentUpdate onReload={onRetry} />;
    const message = error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu";
    return <ErrorState onRetry={onRetry}>{message}</ErrorState>;
  }
  if (isEmpty) return <EmptyState title={emptyTitle} />;
  return <>{children}</>;
}
