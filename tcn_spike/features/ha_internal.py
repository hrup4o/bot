import pandas as pd
import numpy as np
from typing import List

from .ha_external import _rolling_slope, _angle_from_slope


def compute_ha_internal_metrics(ohlcv: pd.DataFrame, windows: List[int]) -> pd.DataFrame:
    """Compute metrics on original close: slope, angle, acceleration for each window."""
    if "close" not in ohlcv.columns:
        raise ValueError("Expected 'close' in OHLCV DataFrame")

    result = pd.DataFrame(index=ohlcv.index)
    for w in windows:
        slope = _rolling_slope(ohlcv["close"], w)
        angle = _angle_from_slope(slope)
        accel = slope.diff()
        result[f"haint_slope_w{w}"] = slope
        result[f"haint_angle_w{w}"] = angle
        result[f"haint_accel_w{w}"] = accel
    return result