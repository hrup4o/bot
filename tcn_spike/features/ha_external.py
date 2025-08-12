import pandas as pd
import numpy as np
from typing import Dict, List


def _rolling_slope(series: pd.Series, window: int) -> pd.Series:
    x = np.arange(window)
    x_mean = x.mean()
    denom = ((x - x_mean) ** 2).sum()

    def slope_calc(values: np.ndarray) -> float:
        y = values
        y_mean = y.mean()
        num = ((x - x_mean) * (y - y_mean)).sum()
        return num / denom if denom != 0 else 0.0

    return series.rolling(window, min_periods=window).apply(slope_calc, raw=True)


def _angle_from_slope(slope: pd.Series) -> pd.Series:
    return np.degrees(np.arctan(slope))


def _rolling_acceleration(series: pd.Series, window: int) -> pd.Series:
    slope = _rolling_slope(series, window)
    return slope.diff()


def compute_ha_external_metrics(ha_df: pd.DataFrame, windows: List[int]) -> pd.DataFrame:
    """Compute metrics on HA close: slope, angle, acceleration for each window.

    Returns a DataFrame with columns like: slope_w{w}, angle_w{w}, accel_w{w}.
    """
    if "ha_close" not in ha_df.columns:
        raise ValueError("Expected 'ha_close' in HA DataFrame")

    result = pd.DataFrame(index=ha_df.index)
    for w in windows:
        slope = _rolling_slope(ha_df["ha_close"], w)
        angle = _angle_from_slope(slope)
        accel = slope.diff()
        result[f"haext_slope_w{w}"] = slope
        result[f"haext_angle_w{w}"] = angle
        result[f"haext_accel_w{w}"] = accel
    return result