import * as React from "react";

function cx(...parts: Array<string | undefined | false | null>) {
  return parts.filter(Boolean).join(" ");
}

export type CardProps = React.HTMLAttributes<HTMLDivElement>;

export function Card({ className, ...props }: CardProps) {
  return (
    <div
      className={cx(
        "rounded-[10px] bg-white shadow-1 dark:bg-gray-dark dark:shadow-card",
        className
      )}
      {...props}
    />
  );
}
