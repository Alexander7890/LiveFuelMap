export const premiumEase = [0.22, 1, 0.36, 1];

export const pageTransition = {
  duration: 0.86,
  ease: premiumEase
};

export const revealVariants = {
  hidden: direction => ({
    opacity: 0,
    x: direction === "left" ? -46 : direction === "right" ? 46 : 0,
    y: direction === "up" ? 42 : direction === "down" ? -30 : 22,
    scale: 0.985,
    filter: "blur(14px)"
  }),
  visible: {
    opacity: 1,
    x: 0,
    y: 0,
    scale: 1,
    filter: "blur(0px)",
    transition: pageTransition
  }
};

export const staggerContainer = {
  hidden: {},
  visible: {
    transition: {
      staggerChildren: 0.095,
      delayChildren: 0.1
    }
  }
};

export const staggerItem = {
  hidden: {
    opacity: 0,
    y: 24,
    scale: 0.985,
    filter: "blur(10px)"
  },
  visible: {
    opacity: 1,
    y: 0,
    scale: 1,
    filter: "blur(0px)",
    transition: {
      duration: 0.68,
      ease: premiumEase
    }
  }
};

export const modalBackdrop = {
  hidden: { opacity: 0 },
  visible: { opacity: 1, transition: { duration: 0.24 } },
  exit: { opacity: 0, transition: { duration: 0.2 } }
};

export const modalPanel = {
  hidden: { opacity: 0, y: 28, scale: 0.96, filter: "blur(10px)" },
  visible: {
    opacity: 1,
    y: 0,
    scale: 1,
    filter: "blur(0px)",
    transition: { duration: 0.48, ease: premiumEase }
  },
  exit: {
    opacity: 0,
    y: 18,
    scale: 0.98,
    filter: "blur(8px)",
    transition: { duration: 0.22 }
  }
};
