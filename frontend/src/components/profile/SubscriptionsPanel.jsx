import { useEffect, useState } from "react";
import { Button, Select } from "../common/Ui";
import { api } from "../../services/api";
import { useData } from "../../contexts/DataContext";
import { useToast } from "../../contexts/ToastContext";

export default function SubscriptionsPanel() {
  const { fuels } = useData();
  const { showToast } = useToast();
  const [items, setItems] = useState([]);
  const [form, setForm] = useState({ fuelId: "", city: "Харків", frequency: "daily" });

  async function load() {
    setItems(await api.subscriptions.list());
  }

  useEffect(() => {
    load().catch(() => {});
  }, []);

  async function submit(event) {
    event.preventDefault();
    try {
      await api.subscriptions.create({ ...form, fuelId: Number(form.fuelId) });
      showToast("Підписку оформлено", "Лист-підтвердження та таблицю цін буде надіслано на email.");
      await load();
    } catch (error) {
      showToast("Підписку не створено", error.message, "danger");
    }
  }

  async function remove(id) {
    await api.subscriptions.delete(id);
    await load();
  }

  return (
    <div className="grid gap-4">
      <form onSubmit={submit} className="panel grid gap-4 p-5">
        <Select label="Тип пального" value={form.fuelId} onChange={event => setForm({ ...form, fuelId: event.target.value })} required>
          <option value="">Оберіть пальне</option>
          {fuels.map(fuel => <option key={fuel.id} value={fuel.id}>{fuel.name}</option>)}
        </Select>
        <Select label="Частота" value={form.frequency} onChange={event => setForm({ ...form, frequency: event.target.value })}>
          <option value="daily">Щодня</option>
          <option value="weekly">Щотижня</option>
        </Select>
        <Button>Підписатися</Button>
      </form>
      <div className="panel p-5">
        <h3 className="font-bold">Мої підписки</h3>
        <div className="mt-3 grid gap-2">
          {items.length === 0 && <p className="text-sm text-slate-500">Підписок поки немає.</p>}
          {items.map(item => (
            <div key={item.id} className="flex items-center justify-between rounded-xl bg-slate-50 p-3 text-sm dark:bg-slate-900">
              <span>{item.fuelName} · {item.city} · {item.frequency}</span>
              <Button variant="secondary" onClick={() => remove(item.id)}>Видалити</Button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

