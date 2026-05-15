import { FileDown } from "lucide-react";
import { useMemo, useState } from "react";
import { Button, Card, Input, Section, Select } from "../components/common/Ui";
import { useData } from "../contexts/DataContext";
import { useToast } from "../contexts/ToastContext";
import { api } from "../services/api";
import { downloadBlob } from "../utils/format";

function dateInput(offsetDays) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function ReportsPage() {
  const { stations, fuels } = useData();
  const { showToast } = useToast();
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState({
    city: "Харків",
    stationIds: [],
    fuelCodes: [],
    from: dateInput(-30),
    to: dateInput(0),
    format: "pdf"
  });

  const stationOptions = useMemo(() => [...stations].sort((a, b) => a.name.localeCompare(b.name, "uk")), [stations]);

  function toggleFuel(code) {
    setForm(current => ({
      ...current,
      fuelCodes: current.fuelCodes.includes(code)
        ? current.fuelCodes.filter(item => item !== code)
        : [...current.fuelCodes, code]
    }));
  }

  function toggleStation(id) {
    setForm(current => ({
      ...current,
      stationIds: current.stationIds.includes(id)
        ? current.stationIds.filter(item => item !== id)
        : [...current.stationIds, id]
    }));
  }

  async function submit(event) {
    event.preventDefault();
    if (loading) return;
    if (!form.fuelCodes.length) {
      showToast("Пальне не вибрано", "Оберіть хоча б один тип пального.", "warning");
      return;
    }
    setLoading(true);
    try {
      const request = {
        city: form.city || null,
        stationIds: form.stationIds.length ? form.stationIds : null,
        fuelCodes: form.fuelCodes,
        from: form.from,
        to: form.to,
        format: form.format
      };
      const { blob, fileName } = await api.report(request);
      downloadBlob(blob, fileName);
      showToast("Звіт сформовано", fileName);
    } catch (error) {
      showToast("Помилка експорту", error.message, "danger");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Section title="Звіт за цінами" subtitle="Backend на C# формує PDF або Excel з таблицями, графіками, діаграмами та автоматичним висновком.">
      <Card as="form" onSubmit={submit} className="grid gap-6">
        <div className="grid gap-4 md:grid-cols-4">
          <Input label="Місто" value={form.city} onChange={event => setForm({ ...form, city: event.target.value })} />
          <Input type="date" label="Дата початку" value={form.from} onChange={event => setForm({ ...form, from: event.target.value })} required />
          <Input type="date" label="Дата завершення" value={form.to} onChange={event => setForm({ ...form, to: event.target.value })} required />
          <Select label="Формат" value={form.format} onChange={event => setForm({ ...form, format: event.target.value })}>
            <option value="pdf">PDF</option>
            <option value="excel">Excel</option>
          </Select>
        </div>

        <div>
          <div className="mb-2 text-sm font-bold">Типи пального</div>
          <div className="flex flex-wrap gap-2">
            {fuels.map(fuel => (
              <button
                key={fuel.code}
                type="button"
                onClick={() => toggleFuel(fuel.code)}
                className={`rounded-full px-3 py-2 text-sm font-semibold transition duration-300 hover:-translate-y-0.5 ${form.fuelCodes.includes(fuel.code) ? "bg-brand-600 text-white shadow-glow" : "bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200"}`}
              >
                {fuel.name}
              </button>
            ))}
          </div>
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between gap-3">
            <div className="text-sm font-bold">АЗС</div>
            <div className="flex gap-2">
              <Button type="button" variant="secondary" onClick={() => setForm({ ...form, stationIds: stationOptions.map(item => item.id) })}>Обрати всі</Button>
              <Button type="button" variant="ghost" onClick={() => setForm({ ...form, stationIds: [] })}>Очистити</Button>
            </div>
          </div>
          <div className="max-h-72 overflow-y-auto rounded-2xl border border-slate-200 p-3 dark:border-slate-800">
            <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
              {stationOptions.map(station => (
                <label key={station.id} className="flex cursor-pointer items-start gap-2 rounded-xl bg-slate-50 p-3 text-sm transition hover:-translate-y-0.5 hover:bg-brand-50 dark:bg-slate-900 dark:hover:bg-brand-500/10">
                  <input type="checkbox" checked={form.stationIds.includes(station.id)} onChange={() => toggleStation(station.id)} className="mt-1" />
                  <span><strong>{station.name}</strong><span className="block text-xs text-slate-500">{station.address}</span></span>
                </label>
              ))}
            </div>
          </div>
        </div>

        <Button type="submit" loading={loading} className="w-fit"><FileDown className="h-4 w-4" /> Сформувати звіт</Button>
      </Card>
    </Section>
  );
}
