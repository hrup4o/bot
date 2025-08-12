import pandas as pd
import numpy as np
from typing import List


def rate_of_change(series: pd.Series, window: int) -> pd.Series:
    return series.pct_change(periods=window)


def rolling_volatility(series: pd.Series, window: int) -> pd.Series:
    return series.pct_change().rolling(window, min_periods=window).std()


def price_strength_index(series: pd.Series, window: int) -> pd.Series:
    """A simple PSI proxy: fraction of up closes over the window."""
    up = (series.diff() > 0).astype(float)
    return up.rolling(window, min_periods=window).mean()


def momentum(series: pd.Series, window: int) -> pd.Series:
    return series.diff(periods=window)


def rolling_zscore(series: pd.Series, window: int) -> pd.Series:
    mean = series.rolling(window, min_periods=window).mean()
    std = series.rolling(window, min_periods=window).std()
    return (series - mean) / (std.replace(0, np.nan))


def compute_indicator_metrics(ohlcv: pd.DataFrame, windows: List[int]) -> pd.DataFrame:
    if "close" not in ohlcv.columns:
        raise ValueError("Expected 'close' in OHLCV DataFrame")

    close = ohlcv["close"]
    result = pd.DataFrame(index=ohlcv.index)
    for w in windows:
        result[f"roc_w{w}"] = rate_of_change(close, w)
        result[f"vol_w{w}"] = rolling_volatility(close, w)
        result[f"psi_w{w}"] = price_strength_index(close, w)
        result[f"mom_w{w}"] = momentum(close, w)
        result[f"zscore_w{w}"] = rolling_zscore(close, w)
    return result