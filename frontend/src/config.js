export const runtimeConfig = window.LiveFuelMapConfig || {};

const isViteDev = import.meta.env.DEV;

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  runtimeConfig.apiBaseUrl ||
  (isViteDev ? "http://localhost:5000" : window.location.origin);

export const SIGNALR_HUB_URL =
  import.meta.env.VITE_SIGNALR_HUB_URL ||
  runtimeConfig.signalRHubUrl ||
  `${API_BASE_URL}/hubs/fuel`;

export const GOOGLE_MAPS_API_KEY =
  import.meta.env.VITE_GOOGLE_MAPS_API_KEY ||
  runtimeConfig.googleMapsApiKey ||
  "";

const adminContactConfig = runtimeConfig.adminContact || {};
const captchaConfig = runtimeConfig.captcha || {};

export const CAPTCHA_PROVIDER =
  import.meta.env.VITE_CAPTCHA_PROVIDER ||
  captchaConfig.provider ||
  "RecaptchaV2";

export const CAPTCHA_SITE_KEY =
  import.meta.env.VITE_CAPTCHA_SITE_KEY ||
  captchaConfig.siteKey ||
  "";

export const CAPTCHA_ENABLED =
  String(import.meta.env.VITE_CAPTCHA_ENABLED ?? captchaConfig.enabled ?? "false").toLowerCase() === "true" &&
  CAPTCHA_PROVIDER.toLowerCase() === "recaptchav2" &&
  Boolean(CAPTCHA_SITE_KEY);

export const ADMIN_CONTACT = {
  email:
    import.meta.env.VITE_ADMIN_CONTACT_EMAIL ||
    adminContactConfig.email ||
    "livefuelmap@gmail.com",
  phone:
    import.meta.env.VITE_ADMIN_CONTACT_PHONE ||
    adminContactConfig.phone ||
    "",
  title: adminContactConfig.title || "",
  description:
    adminContactConfig.description ||
    ""
};

export const GOOGLE_PLACES_SEARCH_AREAS = runtimeConfig.googlePlacesSearchAreas || [
  { name: "Харків", lat: 49.9935, lng: 36.2304, radius: 50000 },
  { name: "Ізюм", lat: 49.2088, lng: 37.2485, radius: 35000 },
  { name: "Лозова", lat: 48.8894, lng: 36.3176, radius: 35000 },
  { name: "Красноград", lat: 49.3801, lng: 35.4567, radius: 35000 },
  { name: "Куп'янськ", lat: 49.7106, lng: 37.6156, radius: 35000 }
];
