// Path: apps/admin/src/components/logo.tsx
export function Logo() {
  return (
    <div className="flex items-center gap-2.5">
      <img
        src="/images/logo/profiqo-icon.png"
        width={36}
        height={36}
        alt="Profiqo"
        className="shrink-0"
      />
      <div className="flex flex-col">
        <span className="text-base font-bold leading-tight text-dark dark:text-white">
          Profiqo
        </span>
        <span className="text-[10px] font-medium leading-tight tracking-wider text-accent">
          SMARTER PROFITS
        </span>
      </div>
    </div>
  );
}