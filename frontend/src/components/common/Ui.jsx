import { AnimatePresence, motion } from "framer-motion";
import { Loader2, X } from "lucide-react";
import { useEffect } from "react";
import { cx } from "../../utils/format";
import { Reveal, SplitText, Stagger, StaggerItem } from "../../motion/MotionPrimitives.jsx";
import { modalBackdrop, modalPanel, premiumEase } from "../../motion/presets.js";

export function Section({ id, title, subtitle, action, children, className }) {
  return (
    <section id={id} className={cx("relative mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8 lg:py-12", className)}>
      {(title || subtitle || action) && (
        <Reveal className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="max-w-4xl">
            {title && (
              <h2 className="text-balance text-2xl font-black tracking-tight text-slate-950 dark:text-white sm:text-3xl">
                <SplitText text={title} />
              </h2>
            )}
            {subtitle && <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600 dark:text-slate-400">{subtitle}</p>}
          </div>
          {action}
        </Reveal>
      )}
      <Reveal delay={0.06}>{children}</Reveal>
    </section>
  );
}

export function Card({ children, className, as: Component = "div", ...props }) {
  return (
    <Component className={cx("panel premium-card p-5", className)} {...props}>
      {children}
    </Component>
  );
}

export function Button({ variant = "primary", className, loading, children, ...props }) {
  const variants = {
    primary: "btn-primary",
    secondary: "btn-secondary",
    ghost: "inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800",
    danger: "inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white transition hover:-translate-y-0.5 hover:bg-red-700 hover:shadow-lg focus:outline-none focus:ring-4 focus:ring-red-500/20 disabled:opacity-55"
  };

  return (
    <motion.button
      whileHover={props.disabled || loading ? undefined : { y: -2, scale: 1.015 }}
      whileTap={props.disabled || loading ? undefined : { scale: 0.985 }}
      transition={{ duration: 0.22, ease: premiumEase }}
      className={cx(variants[variant], className)}
      disabled={loading || props.disabled}
      {...props}
    >
      {loading && <Loader2 className="h-4 w-4 animate-spin" />}
      {children}
    </motion.button>
  );
}

export function Input({ label, className, hint, error, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <input
        className={cx(
          "field",
          error && "border-red-400 bg-red-50/60 pr-10 text-red-950 focus:border-red-500 focus:ring-red-500/20 dark:border-red-500/80 dark:bg-red-950/20 dark:text-red-50"
        )}
        aria-invalid={Boolean(error)}
        {...props}
      />
      {error ? (
        <span className="mt-1.5 block text-xs font-semibold leading-5 text-red-700 dark:text-red-200">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-xs leading-5 text-slate-500 dark:text-slate-400">{hint}</span>
      ) : null}
    </label>
  );
}

export function Select({ label, className, children, error, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <select
        className={cx(
          "field",
          error && "border-red-400 bg-red-50/60 text-red-950 focus:border-red-500 focus:ring-red-500/20 dark:border-red-500/80 dark:bg-red-950/20 dark:text-red-50"
        )}
        aria-invalid={Boolean(error)}
        {...props}
      >
        {children}
      </select>
      {error && <span className="mt-1.5 block text-xs font-semibold leading-5 text-red-700 dark:text-red-200">{error}</span>}
    </label>
  );
}

export function Textarea({ label, className, hint, error, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <textarea
        className={cx(
          "field min-h-[110px] resize-y",
          error && "border-red-400 bg-red-50/60 text-red-950 focus:border-red-500 focus:ring-red-500/20 dark:border-red-500/80 dark:bg-red-950/20 dark:text-red-50"
        )}
        aria-invalid={Boolean(error)}
        {...props}
      />
      {error ? (
        <span className="mt-1.5 block text-xs font-semibold leading-5 text-red-700 dark:text-red-200">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-xs leading-5 text-slate-500 dark:text-slate-400">{hint}</span>
      ) : null}
    </label>
  );
}

export function Modal({
  open,
  title,
  children,
  onClose,
  footer,
  size = "md",
  maxHeight = "94svh",
  bodyMaxHeight = "calc(94svh - 4.5rem)"
}) {
  const sizes = { sm: "max-w-md", md: "max-w-xl", lg: "max-w-3xl", xl: "max-w-5xl" };

  useEffect(() => {
    if (!open) return undefined;

    const originalOverflow = document.body.style.overflow;
    const originalPaddingRight = document.body.style.paddingRight;
    const scrollbarWidth = Math.max(0, window.innerWidth - document.documentElement.clientWidth);

    document.body.style.overflow = "hidden";
    if (scrollbarWidth > 0) {
      document.body.style.paddingRight = `${scrollbarWidth}px`;
    }

    return () => {
      document.body.style.overflow = originalOverflow;
      document.body.style.paddingRight = originalPaddingRight;
    };
  }, [open]);

  function containWheel(event) {
    const container = event.currentTarget;
    const canScroll = container.scrollHeight > container.clientHeight;
    const atTop = container.scrollTop <= 0;
    const atBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 1;

    event.stopPropagation();

    if (!canScroll || (event.deltaY < 0 && atTop) || (event.deltaY > 0 && atBottom)) {
      event.preventDefault();
    }
  }

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          variants={modalBackdrop}
          initial="hidden"
          animate="visible"
          exit="exit"
          className="fixed inset-0 z-[70] grid place-items-center overflow-hidden bg-slate-950/55 p-2 backdrop-blur-md sm:p-4"
          onWheel={event => event.preventDefault()}
        >
          <motion.div
            variants={modalPanel}
            style={{ maxHeight }}
            className={cx("panel w-full max-w-[calc(100vw-1rem)] overflow-hidden", sizes[size])}
          >
            <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3 dark:border-slate-800 sm:px-5 sm:py-4">
              <h3 className="min-w-0 text-base font-bold sm:text-lg">{title}</h3>
              <button className="min-h-11 min-w-11 rounded-full p-2 transition hover:bg-slate-100 dark:hover:bg-slate-800" type="button" onClick={onClose}>
                <X className="h-5 w-5" />
              </button>
            </div>
            <div
              style={{ maxHeight: bodyMaxHeight }}
              className="overflow-y-auto overscroll-contain p-4 sm:p-5"
              onWheel={containWheel}
            >
              {children}
            </div>
            {footer && <div className="border-t border-slate-200 px-4 py-3 dark:border-slate-800 sm:px-5 sm:py-4">{footer}</div>}
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

export function SkeletonGrid({ count = 6 }) {
  return (
    <Stagger className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: count }).map((_, index) => (
        <StaggerItem key={index} className="panel p-5">
          <div className="skeleton h-40" />
          <div className="mt-4 space-y-3">
            <div className="skeleton h-5 w-2/3" />
            <div className="skeleton h-4 w-full" />
            <div className="skeleton h-4 w-5/6" />
          </div>
        </StaggerItem>
      ))}
    </Stagger>
  );
}
