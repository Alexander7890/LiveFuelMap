import { Monitor, Moon, Palette, Sun } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Ui";

const storageKey = "themePreference";

function applyTheme(value) {
  const root = document.documentElement;
  const systemDark = window.matchMedia?.("(prefers-color-scheme: dark)").matches;
  const resolved = value === "system" ? (systemDark ? "dark" : "light") : value;
  root.dataset.theme = resolved;
  root.classList.toggle("dark", resolved === "dark" || resolved === "gray");
}

export default function ThemeMenu({ compact = false }) {
  const { t } = useTranslation();
  const [theme, setTheme] = useState(() => localStorage.getItem(storageKey) || "system");

  useEffect(() => {
    applyTheme(theme);
    localStorage.setItem(storageKey, theme);
  }, [theme]);

  useEffect(() => {
    const media = window.matchMedia?.("(prefers-color-scheme: dark)");
    const listener = () => theme === "system" && applyTheme("system");
    media?.addEventListener("change", listener);
    return () => media?.removeEventListener("change", listener);
  }, [theme]);

  const items = [
    { value: "system", icon: Monitor, label: t("theme.system") },
    { value: "light", icon: Sun, label: t("theme.light") },
    { value: "dark", icon: Moon, label: t("theme.dark") },
    { value: "gray", icon: Palette, label: t("theme.gray") }
  ];

  return (
    <div className={`${compact ? "shrink-0 flex-nowrap" : "w-full flex-wrap"} flex gap-1 rounded-2xl border border-slate-200 bg-white p-1 shadow-sm dark:border-slate-800 dark:bg-slate-900`}>
      {items.map(item => {
        const Icon = item.icon;
        const active = theme === item.value;
        return (
          <Button
            key={item.value}
            variant="ghost"
            className={`${compact ? "px-2" : "flex-1 justify-center px-3"} ${active ? "bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-100" : ""}`}
            onClick={() => setTheme(item.value)}
            title={item.label}
          >
            <Icon className="h-4 w-4" />
            <span className={compact ? "hidden 2xl:inline" : "inline"}>{item.label}</span>
          </Button>
        );
      })}
    </div>
  );
}
