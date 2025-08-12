import pandas as pd
from typing import List


class FeatureAggregator:
    """Aggregates multiple feature DataFrames plus optional HA OHLC columns."""

    def __init__(self, include_ha_ohlc: bool = True) -> None:
        self.include_ha_ohlc = include_ha_ohlc

    def aggregate(self, inputs: List[pd.DataFrame]) -> pd.DataFrame:
        if not inputs:
            raise ValueError("No inputs provided to aggregate")
        df = pd.concat(inputs, axis=1)
        return df