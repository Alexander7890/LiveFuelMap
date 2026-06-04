import { CheckCircle2, Home, Loader2 } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Button, Card, Section } from "../components/common/Ui";
import { useAuth } from "../contexts/AuthContext";
import { useToast } from "../contexts/ToastContext";
import { getAccessToken } from "../services/api";

function isTrue(value) {
  return value === "true" || value === "1";
}

export default function GoogleAuthCallbackPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { completeAuth, refreshProfile } = useAuth();
  const { showToast } = useToast();
  const handledRef = useRef(false);
  const [mode, setMode] = useState("loading");
  const [message, setMessage] = useState(() => t("googleCallback.message"));

  const goHome = useCallback(() => {
    window.location.assign(`${window.location.origin}/`);
    window.setTimeout(() => {
      refreshProfile().catch(() => {});
    }, 0);
  }, [refreshProfile]);

  useEffect(() => {
    async function finish() {
      if (handledRef.current) return;
      handledRef.current = true;

      const params = new URLSearchParams(window.location.hash.replace(/^#/, ""));
      const status = params.get("status");

      try {
        if (!status && getAccessToken()) {
          setMode("success");
          return;
        }

        if (status !== "success") {
          throw new Error(params.get("message") || "Google authentication failed.");
        }

        const token = params.get("token");
        const refreshToken = params.get("refreshToken");
        if (!token || !refreshToken) {
          throw new Error("Google authentication response does not contain JWT tokens.");
        }

        const email = params.get("email");
        const requiresNickname = isTrue(params.get("requiresNickname"));
        await completeAuth({
          token,
          refreshToken,
          userId: params.get("userId"),
          email,
          role: params.get("role"),
          authProvider: params.get("authProvider") || "Google",
          accessTokenExpiresAt: params.get("accessTokenExpiresAt"),
          refreshTokenExpiresAt: params.get("refreshTokenExpiresAt"),
          requiresNickname,
          suggestedNickname: params.get("suggestedNickname")
        }, { verify: false });

        window.history.replaceState(null, "", window.location.pathname);

        if (requiresNickname) {
          showToast(t("auth.successLoginTitle"), t("setupNickname.description"));
          navigate("/setup-nickname", { replace: true });
          return;
        }

        refreshProfile().catch(() => {});
        setMessage(t("googleCallback.message"));
        showToast(t("auth.successLoginTitle"), t("auth.successLoginMessage"));
        setMode("success");
      } catch (finishError) {
        showToast(t("googleCallback.errorTitle"), finishError.message, "danger");
        navigate("/", { replace: true });
      }
    }

    finish();
  }, [completeAuth, navigate, refreshProfile, showToast, t]);

  if (mode === "success") {
    return (
      <Section>
        <Card className="mx-auto grid max-w-md justify-items-center gap-5 overflow-hidden text-center">
          <div className="relative grid h-20 w-20 place-items-center rounded-full bg-brand-50 text-brand-600 shadow-glow dark:bg-brand-500/10">
            <span className="absolute inset-0 rounded-full bg-brand-400/20 animate-ping" />
            <CheckCircle2 className="relative h-10 w-10" />
          </div>
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.22em] text-brand-600 dark:text-brand-200">LiveFuelMap</p>
            <h1 className="mt-2 text-2xl font-black text-slate-950 dark:text-white">{t("auth.successLoginTitle")}</h1>
            <p className="mt-2 text-sm leading-6 text-slate-500">{message}</p>
          </div>
          <Button className="w-full" type="button" onClick={goHome}>
            <Home className="h-4 w-4" />
            {t("googleCallback.home")}
          </Button>
        </Card>
      </Section>
    );
  }

  return (
    <Section>
      <Card className="mx-auto flex max-w-md items-center gap-3">
        <Loader2 className="h-5 w-5 animate-spin text-brand-600" />
        <div>
          <h1 className="text-lg font-black">{t("common.loading")}</h1>
          <p className="text-sm text-slate-500">{t("common.loadingData")}</p>
        </div>
      </Card>
    </Section>
  );
}
