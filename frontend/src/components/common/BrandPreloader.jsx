import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { useEffect, useState } from "react";
import BrandMark from "./BrandMark";
import { premiumEase } from "../../motion/presets";

export default function BrandPreloader() {
  const reduceMotion = useReducedMotion();
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const timer = window.setTimeout(() => setVisible(false), reduceMotion ? 320 : 1180);
    return () => window.clearTimeout(timer);
  }, [reduceMotion]);

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          initial={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.38, ease: premiumEase }}
          className="preloader-screen fixed inset-0 z-[120] grid place-items-center overflow-hidden bg-slate-950 text-white"
        >
          <span className="preloader-orbit preloader-orbit-a" />
          <span className="preloader-orbit preloader-orbit-b" />
          <span className="absolute inset-0 bg-[radial-gradient(circle_at_50%_42%,rgba(148,255,52,.18),transparent_24rem),linear-gradient(180deg,rgba(2,6,23,.12),rgba(2,6,23,.96))]" />
          <motion.div
            initial={{ scale: 0.94, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            exit={{ scale: 1.02, opacity: 0 }}
            transition={{ duration: 0.48, ease: premiumEase }}
            className="relative grid place-items-center gap-6 px-6 text-center"
          >
            <motion.div
              initial={{ rotate: -8, scale: 0.82 }}
              animate={{ rotate: 0, scale: 1 }}
              transition={{ duration: 0.62, ease: premiumEase }}
              className="relative"
            >
              <span className="absolute inset-[-1.25rem] rounded-full bg-lime-400/22 blur-2xl" />
              <BrandMark className="h-24 w-24 rounded-[2rem] shadow-[0_0_70px_rgba(132,255,28,.35)]" />
            </motion.div>
            <div>
              <motion.div
                initial={{ opacity: 0, letterSpacing: "0.04em" }}
                animate={{ opacity: 1, letterSpacing: "0em" }}
                transition={{ delay: 0.12, duration: 0.48, ease: premiumEase }}
                className="text-4xl font-black tracking-tight sm:text-6xl"
              >
                LiveFuelMap
              </motion.div>
              <motion.div
                initial={{ scaleX: 0, opacity: 0 }}
                animate={{ scaleX: 1, opacity: 1 }}
                transition={{ delay: 0.28, duration: 0.42, ease: premiumEase }}
                className="mx-auto mt-5 h-px w-64 origin-center bg-gradient-to-r from-transparent via-lime-300 to-transparent"
              />
              <motion.p
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                transition={{ delay: 0.42, duration: 0.32, ease: premiumEase }}
                className="mt-4 text-sm font-semibold uppercase tracking-[0.32em] text-lime-100/85"
              >
                fuel intelligence
              </motion.p>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
