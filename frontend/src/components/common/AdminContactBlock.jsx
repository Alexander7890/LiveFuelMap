import { CircleHelp, Mail, Phone } from "lucide-react";
import { useTranslation } from "react-i18next";
import { ADMIN_CONTACT } from "../../config";
import { cx } from "../../utils/format";

function phoneHref(phone) {
  const normalized = String(phone || "").replace(/[^\d+]/g, "");
  return normalized ? `tel:${normalized}` : undefined;
}

function gmailHref(email) {
  return `https://mail.google.com/mail/?view=cm&fs=1&to=${encodeURIComponent(email)}`;
}

export default function AdminContactBlock({ className }) {
  const { t } = useTranslation();
  const email = ADMIN_CONTACT.email;
  const phone = ADMIN_CONTACT.phone;
  const title = ADMIN_CONTACT.title || t("contact.title");
  const description = ADMIN_CONTACT.description || t("contact.description");

  return (
    <section className={cx("grid gap-3", className)} aria-labelledby="admin-contact-title">
      <div className="flex items-start gap-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-lime-200">
          <CircleHelp className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <h2 id="admin-contact-title" className="text-base font-black text-slate-950 dark:text-white">
            {title}
          </h2>
          <p className="mt-1 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">
            {description}
          </p>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <a
          className="btn-secondary min-w-0 max-w-full justify-start"
          href={gmailHref(email)}
          target="_blank"
          rel="noreferrer"
        >
          <Mail className="h-4 w-4 shrink-0" />
          <span className="truncate">{email}</span>
        </a>

        {phone ? (
          <a
            className="btn-secondary min-w-0 max-w-full justify-start"
            href={phoneHref(phone)}
          >
            <Phone className="h-4 w-4 shrink-0" />
            <span className="truncate">{phone}</span>
          </a>
        ) : (
          <span className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-600 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-300">
            <Phone className="h-4 w-4 shrink-0" />
            {t("footer.adminFallbackPhone")}
          </span>
        )}
      </div>
    </section>
  );
}
