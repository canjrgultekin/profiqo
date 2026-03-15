import * as React from "react";

type ButtonVariant = "default" | "destructive" | "outline" | "ghost";
type ButtonSize = "default" | "sm" | "lg";

export type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
};

function cx(...parts: Array<string | undefined | false | null>) {
  return parts.filter(Boolean).join(" ");
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = "default", size = "default", disabled, ...props }, ref) => {
    const base =
      "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors " +
      "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 " +
      "disabled:pointer-events-none disabled:opacity-50";

    const variants: Record<ButtonVariant, string> = {
      default:
        "bg-primary text-white hover:bg-primary/90 dark:bg-primary dark:text-white dark:hover:bg-primary/90",
      destructive:
        "bg-red-600 text-white hover:bg-red-600/90 dark:bg-red-600 dark:text-white dark:hover:bg-red-600/90",
      outline:
        "border border-gray-200 bg-white text-gray-900 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100 dark:hover:bg-gray-800",
      ghost:
        "bg-transparent text-gray-900 hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-gray-800",
    };

    const sizes: Record<ButtonSize, string> = {
      default: "h-10 px-4 py-2",
      sm: "h-9 px-3",
      lg: "h-11 px-6",
    };

    return (
      <button
        ref={ref}
        className={cx(base, variants[variant], sizes[size], className)}
        disabled={disabled}
        {...props}
      />
    );
  }
);

Button.displayName = "Button";
