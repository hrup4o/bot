import numpy as np
import pandas as pd
from typing import Optional, Tuple


class StandardScalerTimeSeries:
    """Standard Z-score scaler for time series features.

    Fit on training range only, then apply to entire dataset.
    """

    def __init__(self) -> None:
        self.mean_: Optional[np.ndarray] = None
        self.std_: Optional[np.ndarray] = None
        self.feature_names_: Optional[list[str]] = None

    def fit(self, features: pd.DataFrame, train_slice: slice) -> None:
        train_data = features.iloc[train_slice]
        self.feature_names_ = list(features.columns)
        self.mean_ = train_data.mean(axis=0).values.astype(float)
        std = train_data.std(axis=0).values.astype(float)
        std[std == 0.0] = 1.0
        self.std_ = std

    def transform(self, features: pd.DataFrame) -> pd.DataFrame:
        if self.mean_ is None or self.std_ is None:
            raise RuntimeError("Scaler is not fitted")
        arr = features.values.astype(float)
        scaled = (arr - self.mean_) / self.std_
        return pd.DataFrame(scaled, index=features.index, columns=self.feature_names_)

    def fit_transform(self, features: pd.DataFrame, train_slice: slice) -> pd.DataFrame:
        self.fit(features, train_slice)
        return self.transform(features)

    def get_params(self) -> Tuple[np.ndarray, np.ndarray, list[str]]:
        if self.mean_ is None or self.std_ is None or self.feature_names_ is None:
            raise RuntimeError("Scaler is not fitted")
        return self.mean_, self.std_, self.feature_names_