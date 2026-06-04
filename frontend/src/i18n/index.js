import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";

export const LANGUAGE_STORAGE_KEY = "livefuelmap.language";

export const supportedLanguages = [
  { code: "uk", flag: "🇺🇦", labelKey: "language.uk", nativeName: "Українська" },
  { code: "en", flag: "🇬🇧", labelKey: "language.en", nativeName: "English" },
  { code: "de", flag: "🇩🇪", labelKey: "language.de", nativeName: "Deutsch" },
  { code: "pl", flag: "🇵🇱", labelKey: "language.pl", nativeName: "Polski" }
];

const fallbackLanguage = "uk";
const languageCodes = supportedLanguages.map(item => item.code);
const translationLoaders = import.meta.glob("./locales/*/translation.json");
const loadedLanguages = new Set();

const detectionOptions = {
  order: ["localStorage", "navigator", "htmlTag"],
  lookupLocalStorage: LANGUAGE_STORAGE_KEY,
  caches: ["localStorage"],
  convertDetectedLanguage: lng => normalizeLanguage(lng)
};

export function normalizeLanguage(value) {
  const raw = Array.isArray(value) ? value[0] : value;
  if (!raw || typeof raw !== "string") return fallbackLanguage;

  const normalized = raw.toLowerCase().replace("_", "-");
  const exact = languageCodes.find(code => code === normalized);
  if (exact) return exact;

  const base = normalized.split("-")[0];
  return languageCodes.includes(base) ? base : fallbackLanguage;
}

async function importTranslation(language) {
  const normalized = normalizeLanguage(language);
  const loader = translationLoaders[`./locales/${normalized}/translation.json`];
  if (!loader) {
    return importTranslation(fallbackLanguage);
  }

  const module = await loader();
  return { language: normalized, translation: module.default };
}

export async function loadLanguage(language) {
  const normalized = normalizeLanguage(language);
  if (loadedLanguages.has(normalized) && i18n.hasResourceBundle(normalized, "translation")) {
    return normalized;
  }

  const { translation } = await importTranslation(normalized);
  i18n.addResourceBundle(normalized, "translation", translation, true, true);
  loadedLanguages.add(normalized);
  return normalized;
}

export async function initI18n() {
  if (i18n.isInitialized) return i18n;

  const detector = new LanguageDetector();
  detector.init(detectionOptions);
  const detectedLanguage = normalizeLanguage(detector.detect());

  const [{ translation: fallbackTranslation }, { language: initialLanguage, translation: initialTranslation }] =
    await Promise.all([importTranslation(fallbackLanguage), importTranslation(detectedLanguage)]);

  const resources = {
    [fallbackLanguage]: { translation: fallbackTranslation },
    [initialLanguage]: { translation: initialTranslation }
  };

  loadedLanguages.add(fallbackLanguage);
  loadedLanguages.add(initialLanguage);

  await i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      resources,
      lng: initialLanguage,
      fallbackLng: fallbackLanguage,
      supportedLngs: languageCodes,
      nonExplicitSupportedLngs: true,
      detection: detectionOptions,
      interpolation: {
        escapeValue: false
      },
      react: {
        useSuspense: false
      }
    });

  document.documentElement.lang = initialLanguage;
  return i18n;
}

export default i18n;
