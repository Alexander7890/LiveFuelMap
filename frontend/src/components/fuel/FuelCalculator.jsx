import { Calculator, Gauge, Route } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Card, Input, Select } from "../common/Ui";
import { useData } from "../../contexts/DataContext";
import { formatPrice } from "../../utils/format";

const distancePresets = [50, 100, 150, 200, 300, 400];
const consumptionPresets = [6, 7, 8, 9, 10, 11, 12];

function PresetButton({ active, children, onClick }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`min-h-10 rounded-full px-3 py-1.5 text-xs font-black transition hover:-translate-y-0.5 ${
        active
          ? "bg-brand-600 text-white shadow-glow"
          : "bg-slate-100 text-slate-700 hover:bg-brand-50 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-brand-500/10"
      }`}
    >
      {children}
    </button>
  );
}

export default function FuelCalculator() {
  const { t } = useTranslation();
  const { stations, fuels } = useData();
  const [stationId, setStationId] = useState("");
  const [fuelCode, setFuelCode] = useState("a95");
  const [distance, setDistance] = useState(100);
  const [consumption, setConsumption] = useState(8);
  const [manualPrice, setManualPrice] = useState("");

  const station = stations.find(item => Number(item.id) === Number(stationId)) || stations[0];
  const fuel = fuels.find(item => item.code === fuelCode) || fuels[0];
  const priceSource = useMemo(() => {
    if (manualPrice) return { price: Number(manualPrice), label: t("calculator.manualPriceLabel") };
    const stationPrice = station?.prices?.find(item => item.fuelCode === fuelCode);
    if (stationPrice) return { price: Number(stationPrice.price), label: `${station.name}, ${stationPrice.fuelName}` };
    const networkPrices = stations.flatMap(item => item.prices || []).filter(item => item.fuelCode === fuelCode);
    const avg = networkPrices.reduce((sum, item) => sum + Number(item.price), 0) / Math.max(1, networkPrices.length);
    return Number.isFinite(avg) ? { price: avg, label: t("calculator.averagePriceLabel") } : { price: 0, label: t("calculator.noPriceLabel") };
  }, [manualPrice, station, fuelCode, stations, t]);

  const liters = Number(distance) * Number(consumption) / 100;
  const total = liters * Number(priceSource.price || 0);

  return (
    <Card>
      <div className="flex items-start gap-3 sm:items-center">
        <span className="grid h-11 w-11 place-items-center rounded-2xl bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-100">
          <Calculator className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <h3 className="text-lg font-black">{t("calculator.title")}</h3>
          <p className="text-sm text-slate-500">{t("calculator.subtitle")}</p>
        </div>
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-2">
        <Select label={t("common.stations")} value={station?.id || ""} onChange={event => setStationId(event.target.value)}>
          {stations.map(item => <option key={item.id} value={item.id}>{item.name} · {item.address}</option>)}
        </Select>
        <Select label={t("common.fuel")} value={fuelCode} onChange={event => setFuelCode(event.target.value)}>
          {fuels.map(item => <option key={item.code} value={item.code}>{item.name}</option>)}
        </Select>

        <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-3 dark:border-slate-800 dark:bg-slate-900/50">
          <Input type="number" min="1" label={t("calculator.distance")} value={distance} onChange={event => setDistance(event.target.value)} />
          <div className="mt-3 flex flex-wrap gap-2">
            {distancePresets.map(value => (
              <PresetButton key={value} active={Number(distance) === value} onClick={() => setDistance(value)}>
                <Route className="mr-1 inline h-3.5 w-3.5" /> {value}
              </PresetButton>
            ))}
          </div>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-3 dark:border-slate-800 dark:bg-slate-900/50">
          <Input type="number" min="0.1" step="0.1" label={t("calculator.consumption")} value={consumption} onChange={event => setConsumption(event.target.value)} />
          <div className="mt-3 flex flex-wrap gap-2">
            {consumptionPresets.map(value => (
              <PresetButton key={value} active={Number(consumption) === value} onClick={() => setConsumption(value)}>
                <Gauge className="mr-1 inline h-3.5 w-3.5" /> {value}
              </PresetButton>
            ))}
          </div>
        </div>

        <Input type="number" min="0" step="0.01" label={t("calculator.manualPrice")} value={manualPrice} onChange={event => setManualPrice(event.target.value)} hint={t("calculator.manualPriceHint")} />
      </div>

      <div className="mt-5 grid gap-3 sm:grid-cols-3">
        <div className="soft-panel"><span className="text-sm text-slate-500">{t("calculator.price")}</span><strong className="mt-1 block text-xl">{formatPrice(priceSource.price)} {t("common.currencyShort")}</strong><span className="text-xs text-slate-500">{priceSource.label}</span></div>
        <div className="soft-panel"><span className="text-sm text-slate-500">{t("calculator.fuelNeeded")}</span><strong className="mt-1 block text-xl">{Number.isFinite(liters) ? liters.toFixed(2) : "-"} {t("common.literShort")}</strong><span className="text-xs text-slate-500">{fuel?.name}</span></div>
        <div className="soft-panel"><span className="text-sm text-slate-500">{t("calculator.estimatedCost")}</span><strong className="mt-1 block text-xl text-brand-700 dark:text-brand-200">{formatPrice(total)} {t("common.currencyShort")}</strong><span className="text-xs text-slate-500">{t("calculator.forDistance", { distance: distance || 0 })}</span></div>
      </div>
    </Card>
  );
}
