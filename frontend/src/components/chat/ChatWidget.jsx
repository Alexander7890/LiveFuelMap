import { Bot, Maximize2, Minimize2, Send, Trash2, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import { useTranslation } from "react-i18next";
import { api } from "../../services/api";
import { useToast } from "../../contexts/ToastContext";
import { useAuth } from "../../contexts/AuthContext";
import { useLanguage } from "../../contexts/LanguageContext";
import { premiumEase } from "../../motion/presets";

const guestSessionKey = "chatSessionId";
const scopedSessionKeyPrefix = "chatSessionId:";
const sizeKey = "chatPanelCustomSize";

function createSessionId() {
  return globalThis.crypto?.randomUUID
    ? globalThis.crypto.randomUUID().replaceAll("-", "")
    : `${Date.now()}${Math.random().toString(16).slice(2)}`;
}

function getGuestSessionId() {
  let value = localStorage.getItem(guestSessionKey);
  if (!value) {
    value = createSessionId();
    localStorage.setItem(guestSessionKey, value);
  }
  return value;
}

function getScopedSessionId(scopeKey) {
  if (scopeKey === "guest") return getGuestSessionId();

  const key = `${scopedSessionKeyPrefix}${scopeKey}`;
  let value = localStorage.getItem(key);
  if (!value) {
    value = createSessionId();
    localStorage.setItem(key, value);
  }
  return value;
}

function shouldRequestLocation(message) {
  return /поруч|поблиз|найближ|близьк|геолокац|near|nearby|nearest/i.test(message);
}

function getLocationPayload(message) {
  if (!shouldRequestLocation(message) || !navigator.geolocation) return Promise.resolve({});

  return new Promise(resolve => {
    navigator.geolocation.getCurrentPosition(
      position => resolve({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude
      }),
      () => resolve({}),
      { enableHighAccuracy: false, maximumAge: 5 * 60 * 1000, timeout: 5000 }
    );
  });
}

export default function ChatWidget() {
  const { t } = useTranslation();
  const { showToast } = useToast();
  const { currentUser } = useAuth();
  const { language } = useLanguage();
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [messages, setMessages] = useState([]);
  const [text, setText] = useState("");
  const [size, setSize] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem(sizeKey)) || { width: 410, height: 560 };
    } catch {
      return { width: 410, height: 560 };
    }
  });
  const messagesRef = useRef(null);
  const panelRef = useRef(null);
  const resizing = useRef(null);
  const chatScopeKey = currentUser?.userId ? `user:${currentUser.userId}` : "guest";
  const sessionId = useMemo(() => getScopedSessionId(chatScopeKey), [chatScopeKey]);
  const chatScopeRef = useRef(chatScopeKey);

  useEffect(() => {
    chatScopeRef.current = chatScopeKey;
    setMessages([]);
    setText("");
    setLoading(false);
  }, [chatScopeKey]);

  useEffect(() => {
    if (!open) return;
    let active = true;

    api.chat.history(sessionId)
      .then(items => {
        if (!active) return;
        setMessages((items || []).flatMap(item => [
          { role: "user", text: item.message },
          { role: "bot", text: item.answer }
        ]));
      })
      .catch(() => {});

    return () => {
      active = false;
    };
  }, [open, sessionId, chatScopeKey]);

  useEffect(() => {
    messagesRef.current?.scrollTo({ top: messagesRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  useEffect(() => {
    if (!open) return undefined;

    const panel = panelRef.current;
    const messagesContainer = messagesRef.current;
    if (!panel || !messagesContainer) return undefined;

    const onWheel = event => {
      const isPageDelta = event.deltaMode === 2;
      const isLineDelta = event.deltaMode === 1;
      const multiplier = isPageDelta ? messagesContainer.clientHeight : isLineDelta ? 16 : 1;
      const delta = (Math.abs(event.deltaY) >= Math.abs(event.deltaX) ? event.deltaY : event.deltaX) * multiplier;
      if (!delta) return;

      event.preventDefault();
      event.stopPropagation();
      messagesContainer.scrollTop += delta;
    };

    panel.addEventListener("wheel", onWheel, { passive: false });
    return () => panel.removeEventListener("wheel", onWheel);
  }, [open]);

  useEffect(() => {
    const move = event => {
      if (!resizing.current) return;
      const next = {
        width: Math.min(Math.max(resizing.current.width - (event.clientX - resizing.current.x), 340), Math.min(window.innerWidth - 24, 780)),
        height: Math.min(Math.max(resizing.current.height - (event.clientY - resizing.current.y), 420), Math.min(window.innerHeight - 24, 820))
      };
      setSize(next);
      localStorage.setItem(sizeKey, JSON.stringify(next));
    };
    const up = () => { resizing.current = null; };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    return () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
    };
  }, []);

  async function submit(event) {
    event.preventDefault();
    const message = text.trim();
    if (!message) return;
    const activeScope = chatScopeRef.current;
    setText("");
    setMessages(current => [...current, { role: "user", text: message }]);
    setLoading(true);
    try {
      const locationPayload = await getLocationPayload(message);
      const response = await api.chat.send({ message, sessionId, city: "Харків", language, ...locationPayload });
      if (chatScopeRef.current === activeScope) {
        setMessages(current => [...current, { role: "bot", text: response.answer }]);
      }
    } catch (error) {
      if (chatScopeRef.current === activeScope) {
        setMessages(current => [...current, { role: "bot", text: error?.payload?.answer || t("chat.error") }]);
      }
    } finally {
      if (chatScopeRef.current === activeScope) {
        setLoading(false);
      }
    }
  }

  async function clear() {
    try {
      await api.chat.clear(sessionId);
    } catch {
      // Local clear is still useful.
    }
    setMessages([]);
    showToast(t("chat.clearedTitle"), t("chat.clearedMessage"));
  }

  return (
    <>
      <motion.button
        type="button"
        onClick={() => setOpen(value => !value)}
        whileHover={{ y: -3, scale: 1.05 }}
        whileTap={{ scale: 0.95 }}
        className="fixed bottom-5 right-5 z-[55] grid h-14 w-14 place-items-center rounded-2xl bg-brand-600 text-white shadow-glow transition"
        aria-label={open ? t("common.close") : t("chat.open")}
      >
        {open ? <X className="h-6 w-6" /> : <Bot className="h-6 w-6" />}
      </motion.button>

      <AnimatePresence>
        {open && (
          <motion.section
            ref={panelRef}
            data-lenis-prevent-wheel
            initial={{ opacity: 0, y: 26, scale: 0.96, filter: "blur(12px)" }}
            animate={{ opacity: 1, y: 0, scale: 1, filter: "blur(0px)" }}
            exit={{ opacity: 0, y: 18, scale: 0.98, filter: "blur(8px)" }}
            transition={{ duration: 0.42, ease: premiumEase }}
            className="chat-panel fixed bottom-20 left-3 right-3 z-[54] flex max-h-[calc(100svh-6rem)] flex-col overflow-hidden rounded-3xl border border-slate-200 bg-white/95 shadow-soft backdrop-blur-xl dark:border-slate-800 dark:bg-slate-950/95 sm:bottom-24 sm:left-auto sm:right-5"
            style={{
              width: `min(${size.width}px, calc(100vw - 1.5rem))`,
              height: `min(${size.height}px, calc(100svh - 6rem))`
            }}
          >
            <div className="glass-gradient flex items-center justify-between gap-3 border-b border-white/20 px-4 py-3 text-white">
              <div className="min-w-0">
                <div className="font-bold">{t("chat.title")}</div>
                <div className="truncate text-xs opacity-80">{t("chat.subtitle")}</div>
              </div>
              <div className="flex shrink-0 gap-1">
                <button type="button" onClick={() => setSize({ width: 410, height: 560 })} className="rounded-lg p-2 hover:bg-white/10"><Minimize2 className="h-4 w-4" /></button>
                <button type="button" onClick={() => setSize({ width: 680, height: 720 })} className="rounded-lg p-2 hover:bg-white/10"><Maximize2 className="h-4 w-4" /></button>
                <button type="button" onClick={clear} className="rounded-lg p-2 hover:bg-white/10"><Trash2 className="h-4 w-4" /></button>
                <button type="button" onClick={() => setOpen(false)} className="rounded-lg p-2 hover:bg-white/10"><X className="h-4 w-4" /></button>
              </div>
            </div>
            <div ref={messagesRef} data-lenis-prevent-wheel className="chat-body flex-1 space-y-3 overflow-y-auto overscroll-contain p-4">
              {messages.length === 0 && <div className="rounded-2xl bg-slate-50 p-4 text-sm text-slate-600 dark:bg-slate-900 dark:text-slate-200">{t("chat.empty")}</div>}
              {messages.map((message, index) => (
                <motion.div
                  key={index}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  className={`flex ${message.role === "user" ? "justify-end" : "justify-start"}`}
                >
                  <div className={`max-w-[82%] whitespace-pre-wrap rounded-2xl px-4 py-2 text-sm leading-6 shadow-sm ${message.role === "user" ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-800 dark:bg-slate-900 dark:text-slate-100"}`}>
                    {message.text}
                  </div>
                </motion.div>
              ))}
              {loading && <div className="w-fit rounded-2xl bg-slate-100 px-4 py-2 text-sm dark:bg-slate-900">{t("chat.thinking")}</div>}
            </div>
            <form onSubmit={submit} className="chat-input-bar flex gap-2 border-t border-slate-200 p-2 dark:border-slate-800 sm:p-3">
              <textarea
                className="field max-h-28 min-h-[44px] flex-1 resize-none"
                value={text}
                onChange={event => setText(event.target.value)}
                onKeyDown={event => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    event.currentTarget.form?.requestSubmit();
                  }
                }}
                maxLength={1000}
                placeholder={t("chat.placeholder")}
              />
              <button className="btn-primary h-11 px-3" type="submit" disabled={loading}><Send className="h-4 w-4" /></button>
            </form>
            <div
              className="absolute left-0 top-0 h-5 w-5 cursor-nwse-resize rounded-br-xl bg-brand-600/70"
              onPointerDown={event => {
                event.preventDefault();
                resizing.current = { x: event.clientX, y: event.clientY, width: size.width, height: size.height };
              }}
            />
          </motion.section>
        )}
      </AnimatePresence>
    </>
  );
}
