import { Check, ChevronDown, Languages } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useLanguage } from "../../contexts/LanguageContext";

export default function LanguageSwitcher({ compact = false }) {
  const { t } = useTranslation();
  const { currentLanguage, languages, changeLanguage, isChanging } = useLanguage();
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    const onPointerDown = event => {
      if (!wrapperRef.current?.contains(event.target)) setOpen(false);
    };

    const onKeyDown = event => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={wrapperRef} className={`${compact ? "shrink-0" : "w-full"} relative`}>
      <button
        type="button"
        onClick={() => setOpen(value => !value)}
        disabled={isChanging}
        className={`${compact ? "px-2.5" : "w-full justify-between px-3"} inline-flex min-h-11 items-center gap-2 rounded-2xl border border-slate-200 bg-white/90 py-2 text-sm font-semibold text-slate-800 shadow-sm transition hover:border-brand-300 hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-300 disabled:cursor-wait disabled:opacity-70 dark:border-slate-800 dark:bg-slate-900/90 dark:text-slate-100 dark:hover:border-lime-300/60 dark:hover:bg-lime-300/10`}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={t("language.label")}
        title={t("language.label")}
      >
        <Languages className="h-4 w-4 text-brand-700 dark:text-lime-200" />
        <span className="text-base leading-none" aria-hidden="true">{currentLanguage.flag}</span>
        <span className={compact ? "hidden max-w-24 truncate 2xl:inline" : "max-w-40 truncate"}>{t(currentLanguage.labelKey)}</span>
        <ChevronDown className={`h-4 w-4 transition ${open ? "rotate-180" : ""}`} />
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: 8, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.98 }}
            transition={{ duration: 0.18 }}
            className="absolute right-0 z-[90] mt-2 w-56 overflow-hidden rounded-2xl border border-slate-200 bg-white p-1 shadow-soft dark:border-slate-800 dark:bg-slate-950"
            role="listbox"
            aria-label={t("language.label")}
          >
            {languages.map(item => {
              const active = item.code === currentLanguage.code;
              return (
                <button
                  key={item.code}
                  type="button"
                  onClick={() => {
                    changeLanguage(item.code);
                    setOpen(false);
                  }}
                  className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-semibold transition ${
                    active
                      ? "bg-brand-50 text-brand-800 dark:bg-lime-300/10 dark:text-lime-100"
                      : "text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
                  }`}
                  role="option"
                  aria-selected={active}
                >
                  <span className="text-lg leading-none" aria-hidden="true">{item.flag}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">{t(item.labelKey)}</span>
                    <span className="block truncate text-xs font-medium text-slate-500 dark:text-slate-400">{item.nativeName}</span>
                  </span>
                  {active && <Check className="h-4 w-4" />}
                </button>
              );
            })}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
