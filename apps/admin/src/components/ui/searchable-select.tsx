// Path: apps/admin/src/components/ui/searchable-select.tsx
"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { cn } from "@/lib/utils";

export type SelectOption = {
  value: string;
  label: string;
  description?: string;
};

type SearchableSelectProps = {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  label?: string;
  disabled?: boolean;
  className?: string;
  /** Minimum characters before filtering starts (default 2) */
  minSearchLength?: number;
  /** Allow empty/clearing selection */
  clearable?: boolean;
  /** Empty state message */
  emptyMessage?: string;
};

export function SearchableSelect({
  options,
  value,
  onChange,
  placeholder = "Seçiniz...",
  searchPlaceholder = "Ara...",
  label,
  disabled = false,
  className,
  minSearchLength = 2,
  clearable = false,
  emptyMessage = "Sonuç bulunamadı.",
}: SearchableSelectProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [highlightIndex, setHighlightIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const selectedOption = useMemo(
    () => options.find((o) => o.value === value),
    [options, value]
  );

  const filtered = useMemo(() => {
    if (search.length < minSearchLength) return options;
    const q = search.toLowerCase().trim();
    return options.filter(
      (o) =>
        o.label.toLowerCase().includes(q) ||
        (o.description && o.description.toLowerCase().includes(q))
    );
  }, [options, search, minSearchLength]);

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setSearch("");
        setHighlightIndex(-1);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // Focus input when opened
  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isOpen]);

  // Scroll highlighted item into view
  useEffect(() => {
    if (highlightIndex >= 0 && listRef.current) {
      const el = listRef.current.children[highlightIndex] as HTMLElement;
      if (el) el.scrollIntoView({ block: "nearest" });
    }
  }, [highlightIndex]);

  const handleSelect = useCallback(
    (val: string) => {
      onChange(val);
      setIsOpen(false);
      setSearch("");
      setHighlightIndex(-1);
    },
    [onChange]
  );

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (!isOpen) {
        if (e.key === "Enter" || e.key === "ArrowDown") {
          e.preventDefault();
          setIsOpen(true);
        }
        return;
      }

      switch (e.key) {
        case "ArrowDown":
          e.preventDefault();
          setHighlightIndex((prev) => Math.min(prev + 1, filtered.length - 1));
          break;
        case "ArrowUp":
          e.preventDefault();
          setHighlightIndex((prev) => Math.max(prev - 1, 0));
          break;
        case "Enter":
          e.preventDefault();
          if (highlightIndex >= 0 && highlightIndex < filtered.length) {
            handleSelect(filtered[highlightIndex].value);
          }
          break;
        case "Escape":
          setIsOpen(false);
          setSearch("");
          setHighlightIndex(-1);
          break;
      }
    },
    [isOpen, filtered, highlightIndex, handleSelect]
  );

  return (
    <div ref={containerRef} className={cn("relative", className)}>
      {label && (
        <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
          {label}
        </label>
      )}

      {/* Trigger Button */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (!disabled) setIsOpen(!isOpen);
        }}
        onKeyDown={handleKeyDown}
        className={cn(
          "flex w-full items-center justify-between rounded-lg border bg-transparent px-4 py-2.5 text-left text-sm outline-none transition-colors",
          isOpen
            ? "border-primary"
            : "border-stroke dark:border-dark-3",
          disabled
            ? "cursor-not-allowed opacity-50"
            : "hover:border-dark-6 dark:hover:border-dark-4",
          "text-dark dark:text-white"
        )}
      >
        <span className={cn(!selectedOption && "text-dark-5 dark:text-dark-6")}>
          {selectedOption ? selectedOption.label : placeholder}
        </span>
        <svg
          className={cn(
            "h-4 w-4 shrink-0 text-dark-5 transition-transform dark:text-dark-6",
            isOpen && "rotate-180"
          )}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      {/* Dropdown */}
      {isOpen && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border border-stroke bg-white shadow-3 dark:border-dark-3 dark:bg-gray-dark">
          {/* Search Input */}
          {options.length > 5 && (
            <div className="border-b border-stroke px-3 py-2 dark:border-dark-3">
              <div className="relative">
                <input
                  ref={inputRef}
                  type="text"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setHighlightIndex(-1);
                  }}
                  onKeyDown={handleKeyDown}
                  placeholder={searchPlaceholder}
                  className="w-full rounded border-0 bg-transparent py-1 pl-7 pr-2 text-sm text-dark outline-none dark:text-white"
                />
                <svg
                  className="pointer-events-none absolute left-0 top-1/2 h-4 w-4 -translate-y-1/2 text-dark-6"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth={2}
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
                </svg>
              </div>
            </div>
          )}

          {/* Options List */}
          <div ref={listRef} className="max-h-60 overflow-y-auto py-1">
            {clearable && (
              <button
                type="button"
                onClick={() => handleSelect("")}
                className="flex w-full items-center px-4 py-2 text-left text-sm text-dark-5 transition-colors hover:bg-gray-1 dark:text-dark-6 dark:hover:bg-dark-2"
              >
                Temizle
              </button>
            )}

            {filtered.length === 0 ? (
              <div className="px-4 py-3 text-center text-sm text-dark-5 dark:text-dark-6">
                {search.length > 0 ? `"${search}" için sonuç yok.` : emptyMessage}
              </div>
            ) : (
              filtered.map((option, idx) => (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => handleSelect(option.value)}
                  className={cn(
                    "flex w-full items-center justify-between px-4 py-2 text-left text-sm transition-colors",
                    idx === highlightIndex
                      ? "bg-primary/5 text-primary dark:bg-primary/10"
                      : "text-dark hover:bg-gray-1 dark:text-white dark:hover:bg-dark-2",
                    option.value === value && "font-medium"
                  )}
                >
                  <div>
                    <span>{option.label}</span>
                    {option.description && (
                      <span className="ml-2 text-xs text-dark-5 dark:text-dark-6">
                        {option.description}
                      </span>
                    )}
                  </div>
                  {option.value === value && (
                    <svg className="h-4 w-4 shrink-0 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                    </svg>
                  )}
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}