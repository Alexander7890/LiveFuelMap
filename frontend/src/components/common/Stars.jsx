import { Star } from "lucide-react";
import { normalizeRating } from "../../utils/format";
import { useTranslation } from "react-i18next";

export default function Stars({ rating = 0, interactive = false, onChange }) {
  const { t } = useTranslation();
  const value = normalizeRating(rating);
  return (
    <div className="flex items-center gap-1">
      {[1, 2, 3, 4, 5].map(item => {
        const filled = item <= value;
        if (interactive) {
          return (
            <button
              key={item}
              type="button"
              onClick={() => onChange?.(item)}
              className="rounded p-0.5 text-amber-400 transition hover:scale-110 focus:outline-none focus:ring-2 focus:ring-amber-300"
              aria-label={t("common.starRating", { count: item })}
            >
              <Star className={filled ? "h-5 w-5 fill-current" : "h-5 w-5"} />
            </button>
          );
        }
        return <Star key={item} className={filled ? "h-4 w-4 fill-current text-amber-400" : "h-4 w-4 text-slate-300"} />;
      })}
    </div>
  );
}
