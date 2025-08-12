import pandas as pd
import numpy as np


def compute_heikin_ashi(ohlcv: pd.DataFrame) -> pd.DataFrame:
    """Compute Heikin-Ashi OHLC from raw OHLCV.

    Expects columns: 'open', 'high', 'low', 'close'.
    Returns a DataFrame with columns: 'ha_open', 'ha_high', 'ha_low', 'ha_close'.
    """
    required_columns = {"open", "high", "low", "close"}
    if not required_columns.issubset(ohlcv.columns):
        missing = required_columns - set(ohlcv.columns)
        raise ValueError(f"Missing OHLC columns: {missing}")

    ha = pd.DataFrame(index=ohlcv.index)
    ha["ha_close"] = (
        ohlcv[["open", "high", "low", "close"]].sum(axis=1) / 4.0
    )

    ha_open = np.zeros(len(ohlcv), dtype=float)
    ha_open[0] = (ohlcv["open"].iloc[0] + ohlcv["close"].iloc[0]) / 2.0
    for i in range(1, len(ohlcv)):
        ha_open[i] = (ha_open[i - 1] + ha["ha_close"].iloc[i - 1]) / 2.0
    ha["ha_open"] = ha_open

    ha["ha_high"] = np.maximum.reduce(
        [ohlcv["high"].values, ha["ha_open"].values, ha["ha_close"].values]
    )
    ha["ha_low"] = np.minimum.reduce(
        [ohlcv["low"].values, ha["ha_open"].values, ha["ha_close"].values]
    )

    return ha