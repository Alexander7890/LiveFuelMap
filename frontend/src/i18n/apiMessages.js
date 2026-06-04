import i18n from "./index";

export function translateApiMessage(message, fallback = "") {
  const text = typeof message === "string" ? message.trim() : "";
  if (!text) return fallback;
  return i18n.t(`apiMessages.${text}`, { defaultValue: fallback || text });
}

export function translateApiErrors(errors = {}) {
  return Object.fromEntries(
    Object.entries(errors).map(([field, messages]) => [
      field,
      (Array.isArray(messages) ? messages : [messages]).map(message => translateApiMessage(String(message)))
    ])
  );
}
