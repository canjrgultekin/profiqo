export default function ForbiddenPage() {
  return (
    <div className="p-6">
      <div className="rounded-[10px] bg-white p-6 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h1 className="text-xl font-semibold text-dark dark:text-white">403 - Forbidden</h1>
        <p className="mt-2 text-sm text-body-color dark:text-dark-6">
          Bu sayfayı görüntülemek için yetkin yok.
        </p>
      </div>
    </div>
  );
}
