import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Chrome, XCircle } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Modal, Button, Input } from "../common/Ui";
import CaptchaWidget from "../common/CaptchaWidget";
import { useAuth } from "../../contexts/AuthContext";
import { useToast } from "../../contexts/ToastContext";
import { api } from "../../services/api";
import { CAPTCHA_ENABLED } from "../../config";
import { cx } from "../../utils/format";

const initialForm = {
  email: "",
  password: "",
  confirmPassword: "",
  displayName: "",
  nickname: ""
};

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/i;
const nicknamePattern = /^[\p{L}\p{N}_-]{3,24}$/u;
const obviousPasswords = new Set(["123456", "12345678", "123456789", "password", "qwerty", "111111", "admin", "admin123"]);

function addError(errors, field, message) {
  if (!errors[field]) errors[field] = [];
  errors[field].push(message);
}

function passwordScore(password, t) {
  const value = password || "";
  const checks = [
    { key: "length", label: t("auth.requirements.length"), valid: value.length >= 8 },
    { key: "letters", label: t("auth.requirements.letters"), valid: /[a-zа-яіїєґ]/u.test(value) && /[A-ZА-ЯІЇЄҐ]/u.test(value) },
    { key: "digit", label: t("auth.requirements.digit"), valid: /\d/.test(value) },
    { key: "special", label: t("auth.requirements.special"), valid: /[^A-Za-zА-Яа-яІЇЄҐіїєґ0-9]/u.test(value) },
    { key: "obvious", label: t("auth.requirements.notObvious"), valid: value.length > 0 && !obviousPasswords.has(value.trim().toLowerCase()) }
  ];
  const score = checks.filter(item => item.valid).length;
  if (score <= 2) return { label: t("auth.weakPassword"), tone: "weak", percent: 32, checks };
  if (score <= 4) return { label: t("auth.mediumPassword"), tone: "medium", percent: 66, checks };
  return { label: t("auth.strongPassword"), tone: "strong", percent: 100, checks };
}

function validateForm(form, registerMode, t) {
  const errors = {};
  const email = form.email.trim();

  if (!email) addError(errors, "email", t("auth.errors.emailRequired"));
  else if (!emailPattern.test(email)) addError(errors, "email", t("auth.errors.emailInvalid"));

  if (!form.password) {
    addError(errors, "password", t("auth.errors.passwordRequired"));
  } else if (registerMode && form.password.length < 8) {
    addError(errors, "password", t("auth.errors.passwordMin8"));
  } else if (!registerMode && form.password.length < 6) {
    addError(errors, "password", t("auth.errors.passwordMin6"));
  }

  if (registerMode) {
    const strength = passwordScore(form.password, t);
    const categories = [
      /[a-zа-яіїєґ]/u.test(form.password),
      /[A-ZА-ЯІЇЄҐ]/u.test(form.password),
      /\d/.test(form.password),
      /[^A-Za-zА-Яа-яІЇЄҐіїєґ0-9]/u.test(form.password)
    ].filter(Boolean).length;

    if (form.password && categories < 2) addError(errors, "password", t("auth.errors.passwordComplexity"));
    if (form.password && strength.checks.find(item => item.key === "obvious")?.valid === false) addError(errors, "password", t("auth.errors.passwordObvious"));

    if (!form.confirmPassword) addError(errors, "confirmPassword", t("auth.errors.confirmRequired"));
    else if (form.confirmPassword !== form.password) addError(errors, "confirmPassword", t("auth.errors.confirmMismatch"));

    if (form.displayName.trim().length < 2) addError(errors, "displayName", t("auth.errors.displayNameMin"));

    const nickname = form.nickname.trim();
    if (!nickname) addError(errors, "nickname", t("auth.errors.nicknameRequired"));
    else if (!nicknamePattern.test(nickname)) addError(errors, "nickname", t("auth.errors.nicknameInvalid"));
  }

  return errors;
}

function firstError(errors, field) {
  const value = errors?.[field];
  return Array.isArray(value) ? value[0] : value;
}

function normalizeErrors(errors) {
  return Object.fromEntries(
    Object.entries(errors || {}).map(([key, value]) => [
      key,
      Array.isArray(value) ? value : [String(value)]
    ])
  );
}

export default function AuthModal({ open, onClose }) {
  const { t } = useTranslation();
  const { login, register } = useAuth();
  const { showToast } = useToast();
  const [mode, setMode] = useState("login");
  const [loading, setLoading] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [captchaToken, setCaptchaToken] = useState("");
  const [captchaResetKey, setCaptchaResetKey] = useState(0);
  const [submitted, setSubmitted] = useState(false);
  const [touched, setTouched] = useState({});
  const [serverErrors, setServerErrors] = useState({});
  const [formMessage, setFormMessage] = useState("");
  const [form, setForm] = useState(initialForm);

  const registerMode = mode === "register";
  const strength = useMemo(() => passwordScore(form.password, t), [form.password, t]);
  const clientErrors = useMemo(() => validateForm(form, registerMode, t), [form, registerMode, t]);
  const canSubmit = !loading && Object.keys(clientErrors).length === 0 && (!CAPTCHA_ENABLED || Boolean(captchaToken));

  useEffect(() => {
    if (!open) return;
    setSuggestions([]);
    setSubmitted(false);
    setTouched({});
    setServerErrors({});
    setFormMessage("");
    resetCaptcha();
  }, [open, mode]);

  const handleCaptchaToken = useCallback(token => {
    setCaptchaToken(token);
    setServerErrors(current => {
      if (!current.captchaToken) return current;
      const next = { ...current };
      delete next.captchaToken;
      return next;
    });
  }, []);

  function resetCaptcha() {
    setCaptchaToken("");
    setCaptchaResetKey(key => key + 1);
  }

  function setField(field, value) {
    setForm(current => ({ ...current, [field]: value }));
    setTouched(current => ({ ...current, [field]: true }));
    setServerErrors(current => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
    setFormMessage("");
  }

  function visibleError(field) {
    return firstError(serverErrors, field) || ((submitted || touched[field]) ? firstError(clientErrors, field) : "");
  }

  async function submit(event) {
    event.preventDefault();
    setSubmitted(true);
    setFormMessage("");
    setServerErrors({});

    const validationErrors = validateForm(form, registerMode, t);
    if (Object.keys(validationErrors).length > 0) {
      setFormMessage(t("auth.validationFailed"));
      return;
    }

    if (CAPTCHA_ENABLED && !captchaToken) {
      const message = t("auth.captchaText");
      setServerErrors({ captchaToken: [message] });
      setFormMessage(message);
      return;
    }

    setLoading(true);
    try {
      const captchaPayload = CAPTCHA_ENABLED ? { captchaToken } : {};
      if (mode === "login") {
        await login({ email: form.email.trim(), password: form.password, ...captchaPayload });
        showToast(t("auth.successLoginTitle"), t("auth.successLoginMessage"));
        onClose();
      } else {
        await register({
          ...form,
          email: form.email.trim(),
          displayName: form.displayName.trim(),
          nickname: form.nickname.trim(),
          ...captchaPayload
        });
        showToast(t("auth.successRegisterTitle"), t("auth.successRegisterMessage"));
        setMode("login");
        setForm(current => ({ ...initialForm, email: current.email }));
      }
    } catch (error) {
      const nextErrors = normalizeErrors(error.errors);
      const visibleServerErrors = mode === "login"
        ? Object.fromEntries(Object.entries(nextErrors).filter(([key]) => key.toLowerCase() === "captchatoken"))
        : nextErrors;
      setServerErrors(visibleServerErrors);
      if (error.suggestions?.length) setSuggestions(error.suggestions);
      setFormMessage(error.message || t("auth.actionFailed"));
      showToast(registerMode ? t("auth.registerFailed") : t("auth.loginFailed"), error.message || t("auth.actionFailed"), "danger");
    } finally {
      if (CAPTCHA_ENABLED) resetCaptcha();
      setLoading(false);
    }
  }

  async function loadNicknameSuggestions() {
    if (!form.nickname.trim()) return;
    try {
      const data = await api.auth.nicknameSuggestions(form.nickname);
      setSuggestions(data.suggestions || []);
    } catch {
      setSuggestions([]);
    }
  }

  function startGoogleLogin() {
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    window.location.assign(api.auth.googleStartUrl(returnUrl));
  }

  function switchMode() {
    setMode(registerMode ? "login" : "register");
    setSubmitted(false);
    setTouched({});
    setServerErrors({});
    setFormMessage("");
    setSuggestions([]);
  }

  const strengthColor = {
    weak: "bg-red-500",
    medium: "bg-amber-500",
    strong: "bg-emerald-500"
  }[strength.tone];

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={registerMode ? t("auth.registerTitle") : t("auth.loginTitle")}
      size="md"
      maxHeight={registerMode ? "86svh" : "94svh"}
      bodyMaxHeight={registerMode ? "calc(86svh - 4.25rem)" : "calc(94svh - 4.5rem)"}
    >
      <form onSubmit={submit} className={cx("grid", registerMode ? "gap-3" : "gap-4")} noValidate>
        {formMessage && (
          <div className="rounded-2xl border border-red-200 bg-red-50/90 p-3 text-sm font-semibold leading-6 text-red-800 dark:border-red-500/40 dark:bg-red-950/30 dark:text-red-100">
            {formMessage}
          </div>
        )}

        {registerMode && (
          <>
            <Input
              label={t("auth.displayName")}
              value={form.displayName}
              onChange={event => setField("displayName", event.target.value)}
              error={visibleError("displayName")}
              autoComplete="name"
              required
            />
            <Input
              label={t("auth.nickname")}
              value={form.nickname}
              onBlur={loadNicknameSuggestions}
              onChange={event => setField("nickname", event.target.value)}
              error={visibleError("nickname")}
              required
              hint={t("auth.nicknameHint")}
            />
            {suggestions.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {suggestions.map(item => (
                  <button key={item} type="button" className="badge-soft" onClick={() => setField("nickname", item)}>
                    {item}
                  </button>
                ))}
              </div>
            )}
          </>
        )}

        <Input
          type="email"
          label="Email"
          value={form.email}
          onChange={event => setField("email", event.target.value)}
          error={visibleError("email")}
          autoComplete="email"
          required
        />
        <Input
          type="password"
          label={t("auth.password")}
          value={form.password}
          onChange={event => setField("password", event.target.value)}
          error={visibleError("password")}
          autoComplete={registerMode ? "new-password" : "current-password"}
          required
        />

        {registerMode && (
          <>
            <div className="rounded-2xl border border-slate-200 bg-slate-50/80 p-3 dark:border-slate-700 dark:bg-slate-900/70">
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm font-bold text-slate-800 dark:text-slate-100">{strength.label}</span>
                <span className="text-xs font-semibold text-slate-500 dark:text-slate-300">{strength.percent}%</span>
              </div>
              <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
                <div className={cx("h-full rounded-full transition-all duration-300", strengthColor)} style={{ width: `${strength.percent}%` }} />
              </div>
              <div className="mt-3 grid gap-1.5 text-xs sm:grid-cols-2">
                {strength.checks.map(item => (
                  <span key={item.key} className={cx("inline-flex items-center gap-1.5", item.valid ? "text-emerald-700 dark:text-emerald-200" : "text-slate-500 dark:text-slate-400")}>
                    {item.valid ? <CheckCircle2 className="h-3.5 w-3.5" /> : <XCircle className="h-3.5 w-3.5" />}
                    {item.label}
                  </span>
                ))}
              </div>
            </div>

            <Input
              type="password"
              label={t("auth.confirmPassword")}
              value={form.confirmPassword}
              onChange={event => setField("confirmPassword", event.target.value)}
              error={visibleError("confirmPassword")}
              autoComplete="new-password"
              required
            />
          </>
        )}

        <CaptchaWidget
          resetKey={captchaResetKey}
          onTokenChange={handleCaptchaToken}
          error={firstError(serverErrors, "captchaToken")}
        />

        <Button loading={loading} disabled={!canSubmit}>
          {registerMode ? t("auth.registerButton") : t("auth.loginButton")}
        </Button>

        <div className="relative flex items-center py-1">
          <span className="h-px flex-1 bg-slate-200 dark:bg-slate-800" />
          <span className="px-3 text-xs font-semibold uppercase text-slate-400">Google</span>
          <span className="h-px flex-1 bg-slate-200 dark:bg-slate-800" />
        </div>

        <Button type="button" variant="secondary" className="w-full" onClick={startGoogleLogin} disabled={loading}>
          <Chrome className="h-4 w-4" />
          {registerMode ? t("auth.googleRegister") : t("auth.googleLogin")}
        </Button>

        <button type="button" className="text-sm font-semibold text-brand-700 hover:underline dark:text-brand-200" onClick={switchMode}>
          {registerMode ? t("auth.switchToLogin") : t("auth.switchToRegister")}
        </button>
      </form>
    </Modal>
  );
}
