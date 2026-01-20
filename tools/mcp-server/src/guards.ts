export const assertBinWindow = (startBin: number, endBin: number, maxBins: number): void => {
  if (!Number.isInteger(startBin) || !Number.isInteger(endBin)) {
    throw new Error('startBin and endBin must be integers.');
  }
  if (endBin < startBin) {
    throw new Error('endBin must be >= startBin.');
  }
  const windowSize = endBin - startBin + 1;
  if (windowSize > maxBins) {
    throw new Error(`Requested window ${windowSize} exceeds maxBins ${maxBins}.`);
  }
};
