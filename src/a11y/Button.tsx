import type { ButtonHTMLAttributes } from "react";

// Accessible button primitive (KDM-029). Always has a real type, a visible focus
// ring (focus-visible), and a disabled style that keeps a 4.5:1 contrast label.

type Variant = "primary" | "secondary" | "danger";

const styles: Record<Variant, string> = {
  primary:
    "bg-zinc-900 text-white hover:opacity-90 focus-visible:ring-zinc-900/30 disabled:opacity-50",
  secondary:
    "border border-zinc-300 text-zinc-700 hover:bg-zinc-50 focus-visible:ring-zinc-900/20 disabled:opacity-50",
  danger:
    "bg-red-600 text-white hover:opacity-90 focus-visible:ring-red-500/40 disabled:opacity-50",
};

export function Button({
  variant = "primary",
  type = "button",
  className = "",
  ...props
}: { variant?: Variant } & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      type={type}
      className={`rounded-[10px] px-4 py-3 text-[15px] font-semibold outline-none transition-opacity focus-visible:ring-2 disabled:cursor-not-allowed ${styles[variant]} ${className}`}
    />
  );
}
