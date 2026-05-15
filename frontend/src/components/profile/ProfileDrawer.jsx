import { AnimatePresence, motion } from "framer-motion";
import { Camera, CheckCircle2, LogOut, Mail, Save, Trash2, X } from "lucide-react";
import { useEffect, useState } from "react";
import { Button, Input } from "../common/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { useToast } from "../../contexts/ToastContext";
import { api } from "../../services/api";
import { formatDateTime, imageUrl } from "../../utils/format";
import SubscriptionsPanel from "./SubscriptionsPanel";

export default function ProfileDrawer({ open, onClose }) {
  const { profile, currentUser, refreshProfile, logout } = useAuth();
  const { showToast } = useToast();
  const [tab, setTab] = useState("view");
  const [saving, setSaving] = useState(false);
  const [verifyCode, setVerifyCode] = useState("");
  const [deleteForm, setDeleteForm] = useState({ confirmText: "", password: "" });
  const [form, setForm] = useState({ displayName: "", nickname: "" });
  const data = profile || currentUser || {};

  useEffect(() => {
    if (open && data.emailConfirmed && tab === "verify") {
      setTab("view");
    }
  }, [open, data.emailConfirmed, tab]);

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
      showToast("Профіль оновлено", "Дані користувача збережено.");
      setTab("view");
    } catch (error) {
      showToast("Профіль не оновлено", error.message, "danger");
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
      showToast("Фото оновлено", "Зображення профілю завантажено.");
    } catch (error) {
      showToast("Фото не завантажено", error.message, "danger");
    }
  }

  async function verifyEmail(event) {
    event.preventDefault();
    try {
      await api.auth.verifyEmail(verifyCode);
      await refreshProfile();
      showToast("Email підтверджено", "Акаунт отримав статус верифікованого.");
      setVerifyCode("");
    } catch (error) {
      showToast("Помилка верифікації", error.message, "danger");
    }
  }

  async function resendCode() {
    try {
      await api.auth.resendVerification();
      showToast("Код надіслано", "Перевірте пошту.");
    } catch (error) {
      showToast("Код не надіслано", error.message, "danger");
    }
  }

  async function deleteAccount(event) {
    event.preventDefault();
    try {
      await api.profile.delete(deleteForm);
      showToast("Акаунт видалено", "Ваш профіль видалено.");
      await logout();
      onClose();
    } catch (error) {
      showToast("Не вдалося видалити акаунт", error.message, "danger");
    }
  }

  async function signOut() {
    await logout();
    showToast("Ви вийшли з акаунта", "Сесію завершено на сервері та локально.");
    onClose();
  }

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-[65] bg-slate-950/45 backdrop-blur-sm">
        <motion.aside
          initial={{ x: "100%" }}
          animate={{ x: 0 }}
          exit={{ x: "100%" }}
          transition={{ type: "spring", damping: 28, stiffness: 250 }}
          className="ml-auto h-full w-[min(460px,94vw)] overflow-y-auto bg-white p-5 shadow-soft dark:bg-slate-950"
        >
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-black">Профіль</h2>
            <button className="rounded-full p-2 hover:bg-slate-100 dark:hover:bg-slate-800" onClick={onClose}><X /></button>
          </div>

          <div className="mt-5 panel p-5">
            <div className="flex items-center gap-4">
              <img src={imageUrl(data.profileImageUrl, "/default-station11.jpg")} alt={data.nickname || data.email} className="h-20 w-20 rounded-3xl object-cover" />
              <div className="min-w-0">
                <div className="truncate text-lg font-bold">{data.displayName || data.nickname || data.email}</div>
                <div className="truncate text-sm text-slate-500">{data.email}</div>
                <div className="mt-2 flex flex-wrap gap-2">
                  <span className="badge-soft">{data.role || "User"}</span>
                  <span className={data.emailConfirmed ? "badge-soft bg-emerald-50 text-emerald-700" : "badge-soft bg-amber-50 text-amber-700"}>
                    {data.emailConfirmed ? <CheckCircle2 className="h-3.5 w-3.5" /> : <Mail className="h-3.5 w-3.5" />}
                    {data.emailConfirmed ? "підтверджено" : "не підтверджено"}
                  </span>
                </div>
              </div>
            </div>
            <div className="mt-4 grid gap-2 sm:grid-cols-3">
              <Button variant="secondary" onClick={beginEdit}>Редагувати</Button>
              <label className="btn-secondary cursor-pointer">
                <Camera className="h-4 w-4" /> Фото
                <input className="hidden" type="file" accept="image/*" onChange={uploadPhoto} />
              </label>
              <Button type="button" variant="secondary" onClick={signOut}><LogOut className="h-4 w-4" /> Вийти</Button>
            </div>
          </div>

          <div className="mt-4 flex flex-wrap gap-2">
            {tabs.map(item => (
              <button
                key={item}
                onClick={() => item === "edit" ? beginEdit() : setTab(item)}
                className={`rounded-full px-3 py-1.5 text-sm font-semibold transition ${tab === item ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200 dark:bg-slate-900 dark:text-slate-200"}`}
              >
                {({ view: "Огляд", edit: "Редагування", verify: "Email", subscriptions: "Розсилка", danger: "Видалення" })[item]}
              </button>
            ))}
          </div>

          <div className="mt-4">
            {tab === "view" && (
              <div className="panel grid gap-3 p-5 text-sm">
                <div><span className="text-slate-500">Нікнейм:</span> <strong>{data.nickname || "—"}</strong></div>
                <div><span className="text-slate-500">Створено:</span> <strong>{formatDateTime(data.createdAt)}</strong></div>
                <div><span className="text-slate-500">Останній вхід:</span> <strong>{formatDateTime(data.lastLoginAt)}</strong></div>
              </div>
            )}

            {tab === "edit" && (
              <form onSubmit={saveProfile} className="panel grid gap-4 p-5">
                <Input label="Ім'я" value={form.displayName} onChange={event => setForm({ ...form, displayName: event.target.value })} />
                <Input label="Нікнейм" value={form.nickname} onChange={event => setForm({ ...form, nickname: event.target.value })} />
                <Button loading={saving}><Save className="h-4 w-4" /> Зберегти</Button>
              </form>
            )}

            {tab === "verify" && (
              <form onSubmit={verifyEmail} className="panel grid gap-4 p-5">
                <p className="text-sm text-slate-500">Після підтвердження email можна оформлювати email-розсилку про зміни цін.</p>
                <Input label="Код з email" value={verifyCode} onChange={event => setVerifyCode(event.target.value)} required />
                <div className="grid grid-cols-2 gap-2">
                  <Button>Підтвердити</Button>
                  <Button type="button" variant="secondary" onClick={resendCode}>Надіслати ще раз</Button>
                </div>
              </form>
            )}

            {tab === "subscriptions" && <SubscriptionsPanel />}

            {tab === "danger" && (
              <form onSubmit={deleteAccount} className="panel grid gap-4 border-red-200 p-5 dark:border-red-900/60">
                <p className="text-sm text-red-600">Видалення акаунта незворотне. Для підтвердження введіть DELETE та пароль.</p>
                <Input label="Підтвердження" value={deleteForm.confirmText} onChange={event => setDeleteForm({ ...deleteForm, confirmText: event.target.value })} />
                <Input type="password" label="Пароль" value={deleteForm.password} onChange={event => setDeleteForm({ ...deleteForm, password: event.target.value })} />
                <Button variant="danger"><Trash2 className="h-4 w-4" /> Видалити акаунт</Button>
              </form>
            )}
          </div>
        </motion.aside>
      </div>
    </AnimatePresence>
  );
}
