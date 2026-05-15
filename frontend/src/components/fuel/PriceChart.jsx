import { useEffect, useMemo, useState } from "react";
import { Line } from "react-chartjs-2";
import {
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip
} from "chart.js";
import { api } from "../../services/api";
import { Card, Select } from "../common/Ui";
import { useData } from "../../contexts/DataContext";

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Legend);

export default function PriceChart({ activeFuel }) {
  const { fuels, stations } = useData();
  const [fuel, setFuel] = useState(activeFuel || "all");
  const [stationId, setStationId] = useState("");
  const [data, setData] = useState(null);

  useEffect(() => {
    if (activeFuel && activeFuel !== "all") setFuel(activeFuel);
  }, [activeFuel]);

  useEffect(() => {
    api.priceHistory({ fuel, stationId }).then(setData).catch(() => setData(null));
  }, [fuel, stationId]);

  const chartData = useMemo(() => ({
    labels: data?.labels || [],
    datasets: (data?.datasets || []).map(item => ({
      label: item.label,
      data: item.data,
      borderColor: item.borderColor || "#0f8b8d",
      backgroundColor: item.borderColor || "#0f8b8d",
      tension: Number(item.tension ?? 0.4),
      spanGaps: true,
      pointRadius: 3,
      pointHoverRadius: 6,
      borderWidth: 2.5
    }))
  }), [data]);

  return (
    <Card>
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h3 className="text-lg font-black">Графік зміни цін</h3>
          <p className="text-sm text-slate-500">Лінії не перериваються на днях без оновлення ціни.</p>
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          <Select value={fuel} onChange={event => setFuel(event.target.value)}>
            <option value="all">Усі види</option>
            {fuels.map(item => <option key={item.code} value={item.code}>{item.name}</option>)}
          </Select>
          <Select value={stationId} onChange={event => setStationId(event.target.value)}>
            <option value="">Усі АЗС</option>
            {stations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </Select>
        </div>
      </div>
      <div className="mt-5 h-[360px]">
        <Line
          data={chartData}
          options={{
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 950, easing: "easeOutQuart" },
            interaction: { mode: "index", intersect: false },
            scales: {
              x: { grid: { color: "rgba(148,163,184,.16)" } },
              y: { beginAtZero: false, grid: { color: "rgba(148,163,184,.16)" }, ticks: { callback: value => `${value} грн` } }
            },
            plugins: {
              legend: { position: "bottom" }
            }
          }}
        />
      </div>
    </Card>
  );
}
