import { useEffect, useState } from "react";
import { Modal, Button, Input } from "../common/Ui";
import { useAuth } from "../../contexts/AuthContext";
import { useToast } from "../../contexts/ToastContext";
import { api } from "../../services/api";

export default function AuthModal({ open, onClose }) {
  const { login, register } = useAuth();
  const { showToast } = useToast();
  const [mode, setMode] = useState("login");
  const [loading, setLoading] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [form, setForm] = useState({
    email: "",
    password: "",
    confirmPassword: "",
    displayName: "",
    nickname: ""
  });

  useEffect(() => {
    if (!open) return;
    setSuggestions([]);
  }, [open, mode]);

  async function submit(event) {
    event.preventDefault();
    setLoading(true);
    try {
      if (mode === "login") {
        await login({ email: form.email, password: form.password });
        showToast("Вхід виконано", "JWT отримано від сервера, сесію збережено.");
        onClose();
      } else {
        await register(form);
        showToast("Акаунт створено", "Тепер увійдіть та підтвердьте email кодом.");
        setMode("login");
      }
    } catch (error) {
      showToast("Помилка авторизації", error.message, "danger");
      if (error.suggestions) setSuggestions(error.suggestions);
    } finally {
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

  const registerMode = mode === "register";

  return (
    <Modal open={open} onClose={onClose} title={registerMode ? "Реєстрація" : "Авторизація"}>
      <form onSubmit={submit} className="grid gap-4">
        {registerMode && (
          <>
            <Input label="Ім'я" value={form.displayName} onChange={event => setForm({ ...form, displayName: event.target.value })} required />
            <Input
              label="Нікнейм"
              value={form.nickname}
              onBlur={loadNicknameSuggestions}
              onChange={event => setForm({ ...form, nickname: event.target.value })}
              required
              hint="Якщо нікнейм зайнятий, система запропонує до 3 вільних варіантів."
            />
            {suggestions.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {suggestions.map(item => (
                  <button key={item} type="button" className="badge-soft" onClick={() => setForm({ ...form, nickname: item })}>{item}</button>
                ))}
              </div>
            )}
          </>
        )}
        <Input type="email" label="Email" value={form.email} onChange={event => setForm({ ...form, email: event.target.value })} autoComplete="email" required />
        <Input type="password" label="Пароль" value={form.password} onChange={event => setForm({ ...form, password: event.target.value })} autoComplete={registerMode ? "new-password" : "current-password"} required />
        {registerMode && (
          <Input type="password" label="Повтор пароля" value={form.confirmPassword} onChange={event => setForm({ ...form, confirmPassword: event.target.value })} required />
        )}
        <Button loading={loading}>{registerMode ? "Створити акаунт" : "Увійти"}</Button>
        <button type="button" className="text-sm font-semibold text-brand-700 hover:underline dark:text-brand-200" onClick={() => setMode(registerMode ? "login" : "register")}>
          {registerMode ? "Вже є акаунт? Увійти" : "Немає акаунта? Зареєструватися"}
        </button>
      </form>
    </Modal>
  );
}

