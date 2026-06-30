import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";

export function Brand({ title = "Korkut", subtitle }: { title?: string; subtitle?: string }) {
  return (
    <div className="flex items-center gap-3.5">
      <img
        src="/korkut-logo.svg"
        alt="Korkut"
        className="h-12 w-12 rounded-xl border border-zinc-200 bg-white p-1"
      />
      <div>
        <h1 className="m-0 text-xl font-bold text-zinc-900">{title}</h1>
        {subtitle && <p className="m-0 mt-0.5 text-[13px] text-zinc-500">{subtitle}</p>}
      </div>
    </div>
  );
}

export function AuthShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-zinc-100 p-6">
      <div className="flex w-full max-w-sm flex-col gap-3 rounded-2xl border border-zinc-200 bg-white p-8 shadow-sm">
        {children}
      </div>
    </div>
  );
}

export function Field({ label, ...props }: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[13px] text-zinc-500">{label}</span>
      <input
        {...props}
        className="w-full rounded-[10px] border border-zinc-300 bg-white px-3.5 py-3 text-[15px] text-zinc-900 outline-none focus:border-zinc-900"
      />
    </label>
  );
}

export function Button({ children, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className="rounded-[10px] bg-zinc-900 px-4 py-3 text-[15px] font-semibold text-white transition-opacity hover:opacity-90 disabled:cursor-default disabled:opacity-50"
    >
      {children}
    </button>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  if (!children) return null;
  return <p className="m-0 text-[13px] text-red-600">{children}</p>;
}
