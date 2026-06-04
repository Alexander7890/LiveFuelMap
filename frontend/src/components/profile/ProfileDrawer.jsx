import { AnimatePresence, motion } from "framer-motion";
import { Camera, CheckCircle2, LogOut, Mail, Save, Send, Trash2, X } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button, Input } from "../common/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { useToast } from "../../contexts/ToastContext";
import { api } from "../../services/api";
import { formatDateTime, imageUrl } from "../../utils/format";
import SubscriptionsPanel from "./SubscriptionsPanel";

function containDrawerWheel(event) {
  const container = event.currentTarget;
  const canScroll = container.scrollHeight > container.clientHeight;
  const atTop = container.scrollTop <= 0;
  const atBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 1;

  event.stopPropagation();

  if (!canScroll || (event.deltaY < 0 && atTop) || (event.deltaY > 0 && atBottom)) {
    event.preventDefault();
  }
}

export default function ProfileDrawer({ open, onClose }) {
  const { t } = useTranslation();
  const { profile, currentUser, refreshProfile, logout } = useAuth();
  const { showToast } = useToast();
  const [tab, setTab] = useState("view");
  const [saving, setSaving] = useState(false);
  const [verifyCode, setVerifyCode] = useState("");
  const [deleteForm, setDeleteForm] = useState({ email: "", password: "", verificationCode: "" });
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [deleteCodeLoading, setDeleteCodeLoading] = useState(false);
  const [deleteCodeSent, setDeleteCodeSent] = useState(false);
  const [form, setForm] = useState({ displayName: "", nickname: "" });
  const data = profile || currentUser || {};
  const isGoogleAccount = (data.authProvider || "").toLowerCase() === "google";

  useEffect(() => {
    if (!open) return undefined;

    const originalOverflow = document.body.style.overflow;
    const originalPaddingRight = document.body.style.paddingRight;
    const scrollbarWidth = Math.max(0, window.innerWidth - document.documentElement.clientWidth);
    const lenis = window.__liveFuelMapLenis;

    document.body.style.overflow = "hidden";
    if (scrollbarWidth > 0) {
      document.body.style.paddingRight = `${scrollbarWidth}px`;
    }
    lenis?.stop?.();

    return () => {
      document.body.style.overflow = originalOverflow;
      document.body.style.paddingRight = originalPaddingRight;
      lenis?.start?.();
    };
  }, [open]);

  useEffect(() => {
    if (open && data.emailConfirmed && tab === "verify") setTab("view");
  }, [open, data.emailConfirmed, tab]);

  useEffect(() => {
    if (open && data.email) {
      setDeleteForm(previous => ({ ...previous, email: previous.email || data.email }));
    }
  }, [open, data.email]);

  if (!open) return null;

  const tabs = data.emailConfirmed
    ? ["view", "edit", "subscriptions", "danger"]
    : ["view", "edit", "verify", "subscriptions", "danger"];

  function beginEdit() {
    setForm({
      displayName: data.displayName || "",
      nickname: data.nickname || ""
    });
    setTab("edit");
  }

  async function saveProfile(event) {
    event.preventDefault();
    setSaving(true);
    try {
      await api.profile.update(form);
      await refreshProfile();
      showToast(t("profile.profileUpdated"), t("profile.profileUpdatedMessage"));
      setTab("view");
    } catch (error) {
      showToast(t("profile.profileUpdateFailed"), error.message, "danger");
    } finally {
      setSaving(false);
    }
  }

  async function uploadPhoto(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    try {
      await api.profile.uploadPhoto(file);
      await refreshProfile();
      showToast(t("profile.photoUpdated"), t("profile.photoUpdatedMessage"));
    } catch (error) {
      showToast(t("profile.photoFailed"), error.message, "danger");
    }
  }

  async function verifyEmail(event) {
    event.preventDefault();
    try {
      await api.auth.verifyEmail(verifyCode);
      await refreshProfile();
      showToast(t("profile.emailVerified"), t("profile.emailVerifiedMessage"));
      setVerifyCode("");
    } catch (error) {
      showToast(t("profile.verificationError"), error.message, "danger");
    }
  }

  async function resendCode() {
    try {
      await api.auth.resendVerification();
      showToast(t("profile.codeSent"), t("profile.checkMail"));
    } catch (error) {
      showToast(t("profile.codeNotSent"), error.message, "danger");
    }
  }

  async function requestDeleteCode() {
    setDeleteCodeLoading(true);
    try {
      await api.profile.requestDeletionCode({ email: deleteForm.email });
      setDeleteCodeSent(true);
      showToast(t("profile.codeSent"), t("profile.googleCodeSent"));
    } catch (error) {
      showToast(t("profile.codeNotSent"), error.message, "danger");
    } finally {
      setDeleteCodeLoading(false);
    }
  }

  async function deleteAccount(event) {
    event.preventDefault();
    setDeleteLoading(true);
    try {
      await api.profile.delete(deleteForm);
      showToast(t("profile.accountDeleted"), t("profile.accountDeletedMessage"));
      await logout();
      onClose();
      window.location.assign("/");
    } catch (error) {
      showToast(t("profile.deleteFailed"), error.message, "danger");
    } finally {
      setDeleteLoading(false);
    }
  }

  async function signOut() {
    await logout();
    showToast(t("profile.logoutTitle"), t("profile.logoutMessage"));
    onClose();
  }

  return (
    <AnimatePresence>
      <div
        className="fixed inset-0 z-[65] overflow-hidden bg-slate-950/45 backdrop-blur-sm"
        onWheel={event => event.preventDefault()}
      >
        <motion.aside
          data-lenis-prevent-wheel
          initial={{ x: "100%" }}
          animate={{ x: 0 }}
          exit={{ x: "100%" }}
          transition={{ type: "spring", damping: 28, stiffness: 250 }}
          className="ml-auto h-full w-full overflow-y-auto overscroll-contain bg-white p-4 shadow-soft dark:bg-slate-950 sm:w-[min(460px,94vw)] sm:p-5"
          onWheel={containDrawerWheel}
        >
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-black">{t("profile.title")}</h2>
            <button className="grid min-h-11 min-w-11 place-items-center rounded-full p-2 hover:bg-slate-100 dark:hover:bg-slate-800" onClick={onClose} aria-label={t("common.close")}>
              <X />
            </button>
          </div>

          <div className="mt-5 panel p-5">
            <div className="flex items-start gap-3 sm:items-center sm:gap-4">
              <img src={imageUrl(data.profileImageUrl, "/default-station11.jpg")} alt={data.nickname || data.email} className="h-16 w-16 rounded-2xl object-cover sm:h-20 sm:w-20 sm:rounded-3xl" />
              <div className="min-w-0">
                <div className="truncate text-lg font-bold">{data.displayName || data.nickname || data.email}</div>
                <div className="truncate text-sm text-slate-500 dark:text-slate-300">{data.email}</div>
                <div className="mt-2 flex flex-wrap gap-2">
                  <span className="badge-soft">{data.role || "User"}</span>
                  <span className={data.emailConfirmed ? "badge-soft bg-emerald-50 text-emerald-700" : "badge-soft bg-amber-50 text-amber-700"}>
                    {data.emailConfirmed ? <CheckCircle2 className="h-3.5 w-3.5" /> : <Mail className="h-3.5 w-3.5" />}
                    {data.emailConfirmed ? t("profile.confirmed") : t("profile.notConfirmed")}
                  </span>
                  {data.authProvider && <span className="badge-soft">{data.authProvider}</span>}
                </div>
              </div>
            </div>
            <div className="mt-4 grid gap-2 sm:grid-cols-3">
              <Button variant="secondary" onClick={beginEdit}>{t("common.edit")}</Button>
              <label className="btn-secondary cursor-pointer">
                <Camera className="h-4 w-4" /> {t("profile.photo")}
                <input className="hidden" type="file" accept="image/*" onChange={uploadPhoto} />
              </label>
              <Button type="button" variant="secondary" onClick={signOut}><LogOut className="h-4 w-4" /> {t("common.logout")}</Button>
            </div>
          </div>

          <div className="mt-4 flex flex-wrap gap-2">
            {tabs.map(item => (
              <button
                key={item}
                onClick={() => item === "edit" ? beginEdit() : setTab(item)}
                className={`rounded-full px-3 py-1.5 text-sm font-semibold transition ${tab === item ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200 dark:bg-slate-900 dark:text-slate-200"}`}
              >
                {({ view: t("profile.overview"), edit: t("profile.edit"), verify: t("profile.verifyEmail"), subscriptions: t("profile.subscriptions"), danger: t("profile.danger") })[item]}
              </button>
            ))}
          </div>

          <div className="mt-4">
            {tab === "view" && (
              <div className="panel grid gap-3 p-5 text-sm">
                <div><span className="text-slate-500 dark:text-slate-300">{t("common.nickname")}:</span> <strong>{data.nickname || "-"}</strong></div>
                <div><span className="text-slate-500 dark:text-slate-300">{t("profile.created")}:</span> <strong>{formatDateTime(data.createdAt)}</strong></div>
                <div><span className="text-slate-500 dark:text-slate-300">{t("profile.lastLogin")}:</span> <strong>{formatDateTime(data.lastLoginAt)}</strong></div>
              </div>
            )}

            {tab === "edit" && (
              <form onSubmit={saveProfile} className="panel grid gap-4 p-5">
                <Input label={t("profile.name")} value={form.displayName} onChange={event => setForm({ ...form, displayName: event.target.value })} />
                <Input label={t("common.nickname")} value={form.nickname} onChange={event => setForm({ ...form, nickname: event.target.value })} />
                <Button loading={saving}><Save className="h-4 w-4" /> {t("common.save")}</Button>
              </form>
            )}

            {tab === "verify" && (
              <form onSubmit={verifyEmail} className="panel grid gap-4 p-5">
                <p className="text-sm text-slate-500 dark:text-slate-300">{t("profile.verifyHint")}</p>
                <Input label={t("profile.emailCode")} value={verifyCode} onChange={event => setVerifyCode(event.target.value)} required />
                <div className="grid gap-2 sm:grid-cols-2">
                  <Button>{t("profile.confirmEmail")}</Button>
                  <Button type="button" variant="secondary" onClick={resendCode}>{t("profile.resendCode")}</Button>
                </div>
              </form>
            )}

            {tab === "subscriptions" && <SubscriptionsPanel />}

            {tab === "danger" && (
              <form onSubmit={deleteAccount} className="panel grid gap-4 border-red-200 p-5 dark:border-red-900/60">
                <p className="text-sm text-red-600">
                  {t("profile.deleteWarning")}
                </p>
                <Input
                  label={isGoogleAccount ? t("profile.googleEmail") : t("profile.emailOrNickname")}
                  value={deleteForm.email}
                  onChange={event => setDeleteForm({ ...deleteForm, email: event.target.value })}
                  required
                />

                {isGoogleAccount ? (
                  <>
                    <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
                      <Input
                        label={t("profile.codeFromEmail")}
                        value={deleteForm.verificationCode}
                        onChange={event => setDeleteForm({ ...deleteForm, verificationCode: event.target.value })}
                        required
                      />
                      <Button type="button" variant="secondary" loading={deleteCodeLoading} onClick={requestDeleteCode}>
                        <Send className="h-4 w-4" /> {t("profile.sendCode")}
                      </Button>
                    </div>
                    {deleteCodeSent && <p className="text-xs text-slate-500 dark:text-slate-300">{t("profile.googleCodeSent")}</p>}
                  </>
                ) : (
                  <Input
                    type="password"
                    label={t("common.password")}
                    value={deleteForm.password}
                    onChange={event => setDeleteForm({ ...deleteForm, password: event.target.value })}
                    required
                  />
                )}

                <Button variant="danger" loading={deleteLoading}>
                  <Trash2 className="h-4 w-4" /> {t("profile.deleteAccount")}
                </Button>
              </form>
            )}
          </div>
        </motion.aside>
      </div>
    </AnimatePresence>
  );
}
