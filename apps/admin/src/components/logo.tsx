export function Logo() {
  return (
    <div className="flex items-center gap-2">
      <img
        src="/images/logo/profiqo-icon.png"
        width={36}
        height={36}
        alt="Profiqo"
        className="shrink-0"
      />
      <span className="text-sm font-bold text-slate-400 dark:text-slate-300 whitespace-nowrap">
        Admin Panel
      </span>
    </div>
  );
}