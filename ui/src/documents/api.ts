import { ApiError } from "../api";
import { sessionRequest } from "../session";
import type { DocumentDto } from "./status";

// Document API on the .NET secure-upload contract (KDM-031). All reads/writes flow
// through the session layer (cookie auth, refresh-once). Preview/download is a
// presign the backend only grants for clean objects (KDM-026).

export type LinkTarget = "client" | "matter" | "party";

export type DocumentVersion = {
  version: number;
  checksum_sha256: string;
  parser_version: string | null;
  created_at: string;
};

export const documentsApi = {
  list: (matterId?: string) =>
    sessionRequest<DocumentDto[]>(`/api/documents${matterId ? `?matter_id=${encodeURIComponent(matterId)}` : ""}`),

  /** Presigned download URL — throws ApiError(403/409) if the document is not clean. */
  presign: (id: string) => sessionRequest<{ url: string }>(`/api/documents/${id}/download`),

  link: (id: string, target: LinkTarget, targetId: string, primary = false) =>
    sessionRequest<{ id: string }>(`/api/documents/${id}/links`, {
      method: "POST",
      body: JSON.stringify({ target_type: target, target_id: targetId, is_primary: primary }),
    }),

  unlink: (id: string, target: LinkTarget, targetId: string) =>
    sessionRequest<void>(`/api/documents/${id}/links`, {
      method: "DELETE",
      body: JSON.stringify({ target_type: target, target_id: targetId }),
    }),

  remove: (id: string) => sessionRequest<void>(`/api/documents/${id}`, { method: "DELETE" }),

  reprocess: (id: string) => sessionRequest<{ ok: boolean }>(`/api/documents/${id}/reprocess`, { method: "POST" }),

  versions: (id: string) => sessionRequest<DocumentVersion[]>(`/api/documents/${id}/versions`),
};

/**
 * Upload a file with progress. Uses XHR (fetch can't report upload progress) with
 * cookie credentials; on 401 it refreshes once via the session layer and retries.
 */
export function uploadDocument(
  file: File,
  opts: { matterId?: string; clientId?: string; onProgress?: (pct: number) => void } = {},
): Promise<DocumentDto> {
  return doUpload(file, opts, true);
}

function doUpload(
  file: File,
  opts: { matterId?: string; clientId?: string; onProgress?: (pct: number) => void },
  allowRetry: boolean,
): Promise<DocumentDto> {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    form.append("file", file, file.name);
    if (opts.matterId) form.append("matter_id", opts.matterId);
    if (opts.clientId) form.append("client_id", opts.clientId);

    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/api/documents");
    xhr.withCredentials = true;
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable && opts.onProgress) opts.onProgress(Math.round((e.loaded / e.total) * 100));
    };
    xhr.onload = async () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(JSON.parse(xhr.responseText) as DocumentDto);
      } else if (xhr.status === 401 && allowRetry) {
        const { refreshSession } = await import("../session");
        const ok = await refreshSession();
        if (ok) {
          doUpload(file, opts, false).then(resolve, reject);
        } else {
          reject(new ApiError(401, "Oturum süresi doldu"));
        }
      } else {
        let detail = "Yükleme başarısız";
        try {
          detail = JSON.parse(xhr.responseText)?.detail ?? detail;
        } catch {
          /* keep default */
        }
        reject(new ApiError(xhr.status, detail));
      }
    };
    xhr.onerror = () => reject(new ApiError(0, "Ağ hatası"));
    xhr.send(form);
  });
}
