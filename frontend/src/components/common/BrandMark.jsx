import { motion } from "framer-motion";
import { premiumEase } from "../../motion/presets";
import { cx } from "../../utils/format";

export default function BrandMark({ className, imageClassName }) {
  return (
    <motion.span
      aria-hidden="true"
      whileHover={{ scale: 1.06, rotate: -3 }}
      transition={{ duration: 0.42, ease: premiumEase }}
      className={cx(
        "brand-mark relative grid h-11 w-11 shrink-0 place-items-center overflow-hidden rounded-2xl",
        className
      )}
    >
      <img
        src="/favicon-32x32.png"
        alt=""
        draggable={false}
        className={cx("h-full w-full object-cover", imageClassName)}
      />
    </motion.span>
  );
}
