import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";

export function Brand({ subtitle }: { subtitle?: string }) {
  return (
    <div className="flex items-center gap-3.5">
      <div className="grid h-12 w-12 place-items-center rounded-xl bg-white text-lg font-extrabold tracking-wide text-black">
        CH
      </div>
      <div>
        <h1 className="m-0 text-xl font-bold">Cankar Hukuk</h1>
        {subtitle && <p className="m-0 mt-0.5 text-[13px] text-zinc-400">{subtitle}</p>}
      </div>
    </div>
  );
}

export function AuthShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center p-6">
      <div className="flex w-full max-w-sm flex-col gap-3 rounded-2xl border border-zinc-800 bg-zinc-950 p-8">
        {children}
      </div>
    </div>
  );
}

export function Field({ label, ...props }: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[13px] text-zinc-400">{label}</span>
      <input
        {...props}
        className="w-full rounded-[10px] border border-zinc-800 bg-zinc-900 px-3.5 py-3 text-[15px] text-zinc-100 outline-none focus:border-white"
      />
    </label>
  );
}

export function Button({ children, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className="rounded-[10px] bg-white px-4 py-3 text-[15px] font-semibold text-black transition-opacity hover:opacity-90 disabled:cursor-default disabled:opacity-50"
    >
      {children}
    </button>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  if (!children) return null;
  return <p className="m-0 text-[13px] text-red-400">{children}</p>;
}
