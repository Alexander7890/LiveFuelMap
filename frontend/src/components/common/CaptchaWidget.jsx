import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from "react";
import { AlertCircle, CheckCircle2, Loader2, ShieldCheck } from "lucide-react";
import { useTranslation } from "react-i18next";
import { CAPTCHA_ENABLED, CAPTCHA_SITE_KEY } from "../../config";
import { cx } from "../../utils/format";

const recaptchaScriptId = "google-recaptcha-v2-script";
const recaptchaScriptSrc = "https://www.google.com/recaptcha/api.js?render=explicit";

function resolveWhenReady(resolve, reject) {
  const grecaptcha = window.grecaptcha;
  if (!grecaptcha) {
    reject(new Error("reCAPTCHA script did not initialize."));
    return;
  }

  if (typeof grecaptcha.ready === "function") {
    grecaptcha.ready(() => resolve(grecaptcha));
    return;
  }

  resolve(grecaptcha);
}

function waitForRecaptcha(resolve, reject) {
  let attempts = 0;
  const check = () => {
    if (window.grecaptcha) {
      resolveWhenReady(resolve, reject);
      return;
    }

    attempts += 1;
    if (attempts > 100) {
      reject(new Error("reCAPTCHA script did not initialize."));
      return;
    }

    window.setTimeout(check, 50);
  };

  check();
}

function loadRecaptchaScript() {
  if (window.grecaptcha && document.getElementById(recaptchaScriptId)) {
    return new Promise(resolve => resolveWhenReady(resolve, () => resolve(window.grecaptcha)));
  }

  return new Promise((resolve, reject) => {
    const existing = document.getElementById(recaptchaScriptId);
    if (existing) {
      waitForRecaptcha(resolve, reject);
      existing.addEventListener("load", () => waitForRecaptcha(resolve, reject), { once: true });
      existing.addEventListener("error", reject, { once: true });
      return;
    }

    const script = document.createElement("script");
    script.id = recaptchaScriptId;
    script.src = recaptchaScriptSrc;
    script.async = true;
    script.defer = true;
    script.onload = () => waitForRecaptcha(resolve, reject);
    script.onerror = reject;
    document.head.appendChild(script);
  });
}

function recaptchaTheme() {
  const theme = document.documentElement.dataset.theme;
  return theme === "dark" || theme === "gray" || document.documentElement.classList.contains("dark")
    ? "dark"
    : "light";
}

const CaptchaWidget = forwardRef(function CaptchaWidget({ resetKey, onTokenChange, error }, ref) {
  const { t } = useTranslation();
  const frameRef = useRef(null);
  const containerRef = useRef(null);
  const widgetIdRef = useRef(null);
  const lastResetKeyRef = useRef(resetKey);
  const [loadError, setLoadError] = useState("");
  const [status, setStatus] = useState("loading");
  const [themeKey, setThemeKey] = useState(() => recaptchaTheme());
  const [scale, setScale] = useState(1);

  const resetWidget = useCallback(() => {
    setLoadError("");
    onTokenChange("");

    if (!CAPTCHA_ENABLED) return;

    if (window.grecaptcha && widgetIdRef.current !== null) {
      try {
        window.grecaptcha.reset(widgetIdRef.current);
        setStatus("ready");
        return;
      } catch (captchaError) {
        console.warn("reCAPTCHA widget reset failed.", captchaError);
      }
    }

    setStatus("loading");
  }, [onTokenChange]);

  useImperativeHandle(ref, () => ({ reset: resetWidget }), [resetWidget]);

  useEffect(() => {
    const root = document.documentElement;
    const observer = new MutationObserver(() => setThemeKey(recaptchaTheme()));
    observer.observe(root, { attributes: true, attributeFilter: ["class", "data-theme"] });
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!CAPTCHA_ENABLED) {
      onTokenChange("");
      return undefined;
    }

    let disposed = false;
    setStatus("loading");
    setLoadError("");
    onTokenChange("");

    loadRecaptchaScript()
      .then(grecaptcha => {
        if (disposed || !containerRef.current) return;

        containerRef.current.replaceChildren();
        widgetIdRef.current = grecaptcha.render(containerRef.current, {
          sitekey: CAPTCHA_SITE_KEY,
          theme: themeKey,
          callback: token => {
            if (disposed) return;
            setStatus("verified");
            setLoadError("");
            onTokenChange(token || "");
          },
          "expired-callback": () => {
            if (disposed) return;
            setStatus("expired");
            onTokenChange("");
          },
          "error-callback": () => {
            if (disposed) return;
            setStatus("error");
            onTokenChange("");
          }
        });
        setStatus("ready");
      })
      .catch(captchaError => {
        console.warn("reCAPTCHA v2 could not be loaded.", captchaError);
        if (!disposed) {
          setStatus("error");
          setLoadError(t("captcha.loadFailed"));
        }
      });

    return () => {
      disposed = true;
      if (window.grecaptcha && widgetIdRef.current !== null) {
        try {
          window.grecaptcha.reset(widgetIdRef.current);
        } catch {
          // Google can invalidate widget ids after iframe teardown; the next render creates a fresh widget.
        }
        widgetIdRef.current = null;
      }
      containerRef.current?.replaceChildren();
    };
  }, [onTokenChange, themeKey, t]);

  useEffect(() => {
    if (!CAPTCHA_ENABLED || lastResetKeyRef.current === resetKey) return;
    lastResetKeyRef.current = resetKey;
    resetWidget();
  }, [resetKey, resetWidget]);

  useEffect(() => {
    if (!CAPTCHA_ENABLED || !frameRef.current) return undefined;

    const updateScale = () => {
      const width = Math.max(0, frameRef.current.clientWidth - 16);
      const nextScale = Math.max(0.68, Math.min(width / 304, 1));
      setScale(Number.isFinite(nextScale) ? nextScale : 1);
    };

    updateScale();
    const observer = new ResizeObserver(updateScale);
    observer.observe(frameRef.current);
    window.addEventListener("resize", updateScale);

    return () => {
      observer.disconnect();
      window.removeEventListener("resize", updateScale);
    };
  }, []);

  const statusView = useMemo(() => {
    if (status === "verified") {
      return {
        icon: CheckCircle2,
        text: t("captcha.verified"),
        className: "captcha-status-success bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-200 dark:ring-emerald-400/20"
      };
    }

    if (status === "loading") {
      return {
        icon: Loader2,
        text: t("common.loading"),
        className: "captcha-status-muted bg-slate-100 text-slate-600 ring-slate-200 dark:bg-slate-800 dark:text-slate-200 dark:ring-slate-700",
        spin: true
      };
    }

    if (status === "expired") {
      return {
        icon: AlertCircle,
        text: t("captcha.retry"),
        className: "captcha-status-warning bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-200 dark:ring-amber-400/20"
      };
    }

    return {
      icon: ShieldCheck,
      text: t("captcha.active"),
      className: "captcha-status-info bg-brand-50 text-brand-700 ring-brand-100 dark:bg-brand-500/10 dark:text-brand-100 dark:ring-brand-400/20"
    };
  }, [status, t]);

  if (!CAPTCHA_ENABLED) return null;

  const visibleError = error || loadError || (status === "error" ? t("captcha.failed") : "");
  const StatusIcon = statusView.icon;

  return (
    <div
      className={cx(
        "captcha-card w-full rounded-2xl border bg-slate-50/95 p-3 shadow-sm transition duration-300 dark:bg-slate-950/70 sm:p-4",
        visibleError
          ? "border-red-300 ring-4 ring-red-500/10 dark:border-red-500/70"
          : "border-slate-200 hover:border-brand-300 focus-within:border-brand-500 focus-within:ring-4 focus-within:ring-brand-500/15 dark:border-slate-700 dark:hover:border-brand-500/70"
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-start gap-2">
          <span className="captcha-icon grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-700 dark:bg-brand-500/10 dark:text-brand-100">
            <ShieldCheck className="h-4 w-4" />
          </span>
          <div className="min-w-0">
            <div className="text-sm font-bold text-slate-900 dark:text-white">{t("captcha.security")}</div>
            <p className="mt-0.5 text-xs leading-5 text-slate-500 dark:text-slate-300">
              {t("captcha.confirm")}
            </p>
          </div>
        </div>
        <span className={cx("inline-flex shrink-0 items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-bold ring-1", statusView.className)}>
          <StatusIcon className={cx("h-3.5 w-3.5", statusView.spin && "animate-spin")} />
          {statusView.text}
        </span>
      </div>

      <div ref={frameRef} className="captcha-frame mt-3 w-full overflow-hidden rounded-xl border border-slate-200 bg-slate-100/80 p-2 dark:border-slate-700 dark:bg-slate-900/80">
        <div className="captcha-widget-wrap" style={{ height: `${Math.ceil(78 * scale)}px` }}>
          <div
            className="captcha-widget-host"
            ref={containerRef}
            style={{ transform: `scale(${scale})` }}
          />
        </div>
      </div>

      {visibleError && (
        <p className="mt-2 flex items-start gap-2 text-sm font-semibold leading-5 text-red-700 dark:text-red-200">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          {visibleError}
        </p>
      )}
    </div>
  );
});

export default CaptchaWidget;
