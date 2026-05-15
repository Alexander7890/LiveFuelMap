import { AnimatePresence, motion } from "framer-motion";
import { Loader2, X } from "lucide-react";
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
    ghost: "inline-flex items-center justify-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800",
    danger: "inline-flex items-center justify-center gap-2 rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white transition hover:-translate-y-0.5 hover:bg-red-700 hover:shadow-lg focus:outline-none focus:ring-4 focus:ring-red-500/20 disabled:opacity-55"
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

export function Input({ label, className, hint, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <input className="field" {...props} />
      {hint && <span className="mt-1 block text-xs text-slate-500">{hint}</span>}
    </label>
  );
}

export function Select({ label, className, children, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <select className="field" {...props}>{children}</select>
    </label>
  );
}

export function Textarea({ label, className, hint, ...props }) {
  return (
    <label className={cx("block", className)}>
      {label && <span className="mb-1.5 block text-sm font-semibold text-slate-700 dark:text-slate-200">{label}</span>}
      <textarea className="field min-h-[110px] resize-y" {...props} />
      {hint && <span className="mt-1 block text-xs text-slate-500">{hint}</span>}
    </label>
  );
}

export function Modal({ open, title, children, onClose, footer, size = "md" }) {
  const sizes = { sm: "max-w-md", md: "max-w-xl", lg: "max-w-3xl", xl: "max-w-5xl" };
  return (
    <AnimatePresence>
      {open && (
        <motion.div
          variants={modalBackdrop}
          initial="hidden"
          animate="visible"
          exit="exit"
          className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/55 p-4 backdrop-blur-md"
        >
          <motion.div
            variants={modalPanel}
            className={cx("panel max-h-[92vh] w-full overflow-hidden", sizes[size])}
          >
            <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4 dark:border-slate-800">
              <h3 className="text-lg font-bold">{title}</h3>
              <button className="rounded-full p-2 transition hover:bg-slate-100 dark:hover:bg-slate-800" type="button" onClick={onClose}>
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="max-h-[70vh] overflow-y-auto p-5">{children}</div>
            {footer && <div className="border-t border-slate-200 px-5 py-4 dark:border-slate-800">{footer}</div>}
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
