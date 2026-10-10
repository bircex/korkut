import React from "react";
import {
  FilePdf,
  FileDoc,
  FileImage,
  FileCode,
  FileZip,
  FileText,
  File as FileIcon,
  X,
  Icon,
} from "../icons";

interface FileAttachmentCardProps {
  fileName: string;
  fileSize?: number;
  mode?: "draft" | "bubble";
  onRemove?: () => void;
}

type FileConfig = {
  icon: Icon;
  label: string;
  badgeBg: string;
  badgeText: string;
  badgeBorder: string;
};

function getFileConfig(fileName: string): FileConfig {
  const ext = fileName.split(".").pop()?.toLowerCase() || "";

  switch (ext) {
    case "pdf":
      return {
        icon: FilePdf,
        label: "PDF",
        badgeBg: "bg-red-500/15 dark:bg-red-500/20",
        badgeText: "text-red-500 dark:text-red-400",
        badgeBorder: "border-red-500/20",
      };
    case "doc":
    case "docx":
      return {
        icon: FileDoc,
        label: "Word",
        badgeBg: "bg-blue-500/15 dark:bg-blue-500/20",
        badgeText: "text-blue-500 dark:text-blue-400",
        badgeBorder: "border-blue-500/20",
      };
    case "png":
    case "jpg":
    case "jpeg":
    case "webp":
    case "gif":
    case "svg":
      return {
        icon: FileImage,
        label: "Görsel",
        badgeBg: "bg-purple-500/15 dark:bg-purple-500/20",
        badgeText: "text-purple-500 dark:text-purple-400",
        badgeBorder: "border-purple-500/20",
      };
    case "json":
    case "xml":
    case "html":
    case "css":
    case "js":
    case "ts":
      return {
        icon: FileCode,
        label: "Kod",
        badgeBg: "bg-amber-500/15 dark:bg-amber-500/20",
        badgeText: "text-amber-500 dark:text-amber-400",
        badgeBorder: "border-amber-500/20",
      };
    case "zip":
    case "rar":
    case "7z":
    case "tar":
    case "gz":
      return {
        icon: FileZip,
        label: "Arşiv",
        badgeBg: "bg-emerald-500/15 dark:bg-emerald-500/20",
        badgeText: "text-emerald-500 dark:text-emerald-400",
        badgeBorder: "border-emerald-500/20",
      };
    case "txt":
    case "md":
    case "csv":
      return {
        icon: FileText,
        label: "Metin",
        badgeBg: "bg-zinc-500/15 dark:bg-zinc-500/20",
        badgeText: "text-zinc-400 dark:text-zinc-300",
        badgeBorder: "border-zinc-500/20",
      };
    default:
      return {
        icon: FileIcon,
        label: ext.toUpperCase() || "Dosya",
        badgeBg: "bg-zinc-500/15 dark:bg-zinc-500/20",
        badgeText: "text-zinc-400 dark:text-zinc-300",
        badgeBorder: "border-zinc-500/20",
      };
  }
}

function formatSize(bytes?: number): string | null {
  if (!bytes || bytes <= 0) return null;
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export const FileAttachmentCard: React.FC<FileAttachmentCardProps> = ({
  fileName,
  fileSize,
  mode = "bubble",
  onRemove,
}) => {
  const config = getFileConfig(fileName);
  const IconComponent = config.icon;
  const formattedSize = formatSize(fileSize);

  if (mode === "draft") {
    return (
      <div className="group relative flex max-w-[260px] items-center gap-2.5 rounded-lg border border-zinc-200/90 bg-white/95 px-2.5 py-1.5 shadow-sm backdrop-blur-sm dark:border-zinc-700/90 dark:bg-zinc-800/95">
        {/* Sol İkon Kutusu */}
        <div
          className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-md border ${config.badgeBg} ${config.badgeText} ${config.badgeBorder}`}
        >
          <IconComponent size={16} weight="bold" />
        </div>

        {/* Metin Bilgisi */}
        <div className="min-w-0 flex-1">
          <p className="truncate text-[12px] font-medium leading-tight text-zinc-900 dark:text-zinc-100" title={fileName}>
            {fileName}
          </p>
          <div className="flex items-center gap-1 text-[10px] text-zinc-500 dark:text-zinc-400">
            <span>{config.label}</span>
            {formattedSize && (
              <>
                <span>•</span>
                <span className="font-mono">{formattedSize}</span>
              </>
            )}
          </div>
        </div>

        {/* Kaldır Butonu */}
        {onRemove && (
          <button
            type="button"
            onClick={onRemove}
            title="Kaldır"
            className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-zinc-400 hover:bg-zinc-100 hover:text-red-500 dark:hover:bg-zinc-700 dark:hover:text-red-400 transition-colors"
          >
            <X size={12} weight="bold" />
          </button>
        )}
      </div>
    );
  }

  // mode === "bubble" (Sohbet Balonu İçindeki Kompakt Görünüm)
  return (
    <div className="flex max-w-[240px] items-center gap-2.5 rounded-lg border border-white/15 bg-white/10 px-2.5 py-1.5 shadow-sm backdrop-blur-sm transition-all hover:bg-white/15">
      {/* Sol İkon Kutusu */}
      <div
        className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-md border ${config.badgeBg} ${config.badgeText} ${config.badgeBorder}`}
      >
        <IconComponent size={16} weight="bold" />
      </div>

      {/* Bilgi */}
      <div className="min-w-0 flex-1">
        <p className="truncate text-[12px] font-medium leading-tight text-zinc-100" title={fileName}>
          {fileName}
        </p>
        <div className="flex items-center gap-1 text-[10px] text-zinc-300/80">
          <span>{config.label}</span>
          {formattedSize && (
            <>
              <span>•</span>
              <span className="font-mono">{formattedSize}</span>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
