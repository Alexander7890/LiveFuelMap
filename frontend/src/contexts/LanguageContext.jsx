import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import i18n, { LANGUAGE_STORAGE_KEY, loadLanguage, normalizeLanguage, supportedLanguages } from "../i18n";

const LanguageContext = createContext(null);

export function LanguageProvider({ children }) {
  const { t } = useTranslation();
  const [language, setLanguage] = useState(() => normalizeLanguage(i18n.language || localStorage.getItem(LANGUAGE_STORAGE_KEY)));
  const [isChanging, setIsChanging] = useState(false);

  useEffect(() => {
    const onLanguageChanged = nextLanguage => {
      const normalized = normalizeLanguage(nextLanguage);
      document.documentElement.lang = normalized;
      localStorage.setItem(LANGUAGE_STORAGE_KEY, normalized);
      setLanguage(normalized);
    };

    i18n.on("languageChanged", onLanguageChanged);
    onLanguageChanged(i18n.language);
    return () => i18n.off("languageChanged", onLanguageChanged);
  }, []);

  const changeLanguage = useCallback(async nextLanguage => {
    const normalized = normalizeLanguage(nextLanguage);
    if (normalized === normalizeLanguage(i18n.language)) return;

    setIsChanging(true);
    try {
      await loadLanguage(normalized);
      await i18n.changeLanguage(normalized);
    } finally {
      setIsChanging(false);
    }
  }, []);

  const currentLanguage = useMemo(
    () => supportedLanguages.find(item => item.code === language) || supportedLanguages[0],
    [language]
  );

  const value = useMemo(
    () => ({
      language,
      currentLanguage,
      languages: supportedLanguages,
      isChanging,
      changeLanguage,
      t
    }),
    [changeLanguage, currentLanguage, isChanging, language, t]
  );

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage() {
  const context = useContext(LanguageContext);
  if (!context) throw new Error("useLanguage must be used inside LanguageProvider");
  return context;
}
