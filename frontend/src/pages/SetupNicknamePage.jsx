import { CheckCircle2, UserRound } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Button, Card, Input, Section } from "../components/common/Ui";
import { useAuth } from "../contexts/AuthContext";
import { useToast } from "../contexts/ToastContext";
import { api } from "../services/api";

const nicknamePattern = /^[\p{L}\p{N}_-]{3,24}$/u;

function suggestFromEmail(email) {
  const localPart = (email || "").split("@")[0] || "";
  const cleaned = localPart
    .trim()
    .replace(/[^\p{L}\p{N}_-]+/gu, "")
    .replace(/[-_]{2,}/g, "-")
    .replace(/^[-_]+|[-_]+$/g, "")
    .slice(0, 24);

  return cleaned.length >= 3 ? cleaned : "";
}

function validateNickname(value, t) {
  const trimmed = value.trim();
  if (!trimmed) return t("setupNickname.required");
  if (trimmed !== value) return t("setupNickname.trim");
  if (!nicknamePattern.test(trimmed)) {
    return t("setupNickname.invalid");
  }

  return "";
}

export default function SetupNicknamePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { currentUser, profile, refreshProfile } = useAuth();
  const { showToast } = useToast();
  const [nickname, setNickname] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const suggestedNickname = useMemo(
    () => suggestFromEmail(profile?.email || currentUser?.email),
    [currentUser?.email, profile?.email]
  );

  async function handleSubmit(event) {
    event.preventDefault();
    const validationError = validateNickname(nickname, t);
    if (validationError) {
      setError(validationError);
      return;
    }

    setSaving(true);
    setError("");

    try {
      await api.profile.setupNickname({ nickname: nickname.trim() });
      await refreshProfile();
      showToast(t("setupNickname.readyTitle"), t("setupNickname.readyMessage"));
      navigate("/", { replace: true });
    } catch (setupError) {
      setError(setupError.message || t("setupNickname.saveFailed"));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Section className="grid min-h-[calc(100svh-9rem)] place-items-center">
      <Card as="form" onSubmit={handleSubmit} className="mx-auto grid w-full max-w-lg gap-5 text-center">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-brand-50 text-brand-600 shadow-glow dark:bg-brand-500/10 dark:text-brand-200">
          <UserRound className="h-8 w-8" />
        </div>

        <div>
          <p className="text-xs font-bold uppercase tracking-[0.22em] text-brand-600 dark:text-brand-200">LiveFuelMap</p>
          <h1 className="mt-2 text-2xl font-black text-slate-950 dark:text-white">{t("setupNickname.title")}</h1>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-slate-300">
            {t("setupNickname.description")}
          </p>
        </div>

        <Input
          label={t("setupNickname.publicNickname")}
          value={nickname}
          onChange={event => {
            setNickname(event.target.value);
            if (error) setError("");
          }}
          placeholder={suggestedNickname || "MyNickname"}
          autoFocus
          minLength={3}
          maxLength={24}
          autoComplete="nickname"
          hint={t("setupNickname.hint")}
          required
        />

        {suggestedNickname && !nickname && (
          <button
            type="button"
            className="mx-auto text-sm font-semibold text-brand-700 underline-offset-4 transition hover:underline dark:text-brand-200"
            onClick={() => setNickname(suggestedNickname)}
          >
            {t("setupNickname.useSuggestion", { nickname: suggestedNickname })}
          </button>
        )}

        {error && (
          <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-200">
            {error}
          </div>
        )}

        <Button type="submit" className="w-full" loading={saving}>
          {!saving && <CheckCircle2 className="h-4 w-4" />}
          {t("setupNickname.save")}
        </Button>
      </Card>
    </Section>
  );
}
