import numpy as np
import pandas as pd
from typing import Tuple


def generate_entry_exit_labels(
    close: pd.Series,
    horizon: int = 5,
    entry_threshold: float = 0.01,
    exit_threshold: float = 0.01,
) -> pd.DataFrame:
    """Generate entry/exit labels using future window extrema of returns.

    entry=1 if max future return over horizon >= entry_threshold.
    exit=1 if min future return over horizon <= -exit_threshold.
    rest=0 otherwise (both are 0).
    """
    n = len(close)
    forward_max = np.full(n, np.nan, dtype=float)
    forward_min = np.full(n, np.nan, dtype=float)

    values = close.values.astype(float)
    for i in range(n):
        j = min(n, i + horizon + 1)
        if j - i <= 1:
            continue
        window = values[i + 1 : j]
        base = values[i]
        rets = window / base - 1.0
        forward_max[i] = np.max(rets)
        forward_min[i] = np.min(rets)

    entry = (forward_max >= entry_threshold).astype(float)
    exit = (forward_min <= -exit_threshold).astype(float)

    labels = pd.DataFrame({"entry": entry, "exit": exit}, index=close.index)
    labels = labels.fillna(0.0)
    return labels