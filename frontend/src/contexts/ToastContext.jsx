import { createContext, useCallback, useContext, useMemo, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import { AlertCircle, CheckCircle2, Info, X, XCircle } from "lucide-react";
import { cx } from "../utils/format";
import { premiumEase } from "../motion/presets";

const ToastContext = createContext(null);

const toneMap = {
  success: { icon: CheckCircle2, className: "border-emerald-200 bg-emerald-50/95 text-emerald-950" },
  danger: { icon: XCircle, className: "border-red-200 bg-red-50/95 text-red-950" },
  warning: { icon: AlertCircle, className: "border-amber-200 bg-amber-50/95 text-amber-950" },
  info: { icon: Info, className: "border-brand-200 bg-brand-50/95 text-brand-950" }
};

export function ToastProvider({ children }) {
  const [items, setItems] = useState([]);

  const showToast = useCallback((title, message, tone = "success") => {
    const id = crypto?.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    setItems(current => [...current, { id, title, message, tone }]);
    window.setTimeout(() => {
      setItems(current => current.filter(item => item.id !== id));
    }, 5200);
  }, []);

  const dismiss = useCallback(id => {
    setItems(current => current.filter(item => item.id !== id));
  }, []);

  const value = useMemo(() => ({ showToast }), [showToast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed right-4 top-4 z-[80] flex w-[min(420px,calc(100vw-2rem))] flex-col gap-3">
        <AnimatePresence>
          {items.map(item => {
            const tone = toneMap[item.tone] || toneMap.success;
            const Icon = tone.icon;
            return (
              <motion.div
                key={item.id}
                initial={{ opacity: 0, x: 34, y: -8, scale: 0.96, filter: "blur(8px)" }}
                animate={{ opacity: 1, x: 0, y: 0, scale: 1, filter: "blur(0px)" }}
                exit={{ opacity: 0, x: 34, scale: 0.96, filter: "blur(8px)" }}
                transition={{ duration: 0.38, ease: premiumEase }}
                className={cx("premium-card rounded-panel border p-4 shadow-soft backdrop-blur-xl", tone.className)}
              >
                <div className="flex gap-3">
                  <Icon className="mt-0.5 h-5 w-5 shrink-0" />
                  <div className="min-w-0 flex-1">
                    <div className="font-semibold">{item.title}</div>
                    {item.message && <div className="mt-1 text-sm opacity-80">{item.message}</div>}
                  </div>
                  <button
                    className="rounded-full p-1 opacity-65 transition hover:bg-black/5 hover:opacity-100"
                    type="button"
                    onClick={() => dismiss(item.id)}
                    aria-label="Закрити повідомлення"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used inside ToastProvider");
  return context;
}
